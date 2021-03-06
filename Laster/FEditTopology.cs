﻿using Laster.Controls;
using Laster.Core.Classes;
using Laster.Core.Classes.Collections;
using Laster.Core.Enums;
using Laster.Core.Forms;
using Laster.Core.Helpers;
using Laster.Core.Interfaces;
using Laster.Inputs;
using Laster.Process.Developer;
using Laster.Remembers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Pr = System.Diagnostics;

namespace Laster
{
    public partial class FEditTopology : FRememberForm<RememberEditTopology>
    {
        string _LastFile = null;
        public string LastFile
        {
            get { return _LastFile; }
            set
            {
                _LastFile = value;
                if (string.IsNullOrEmpty(_LastFile))
                {
                    Text = "Laster " + (IntPtr.Size == 8 ? "x64" : "x86");
                    saveToolStripMenuItem.Enabled = false;
                }
                else
                {
                    Text = "Laster " + (IntPtr.Size == 8 ? "x64" : "x86") + " [" + _LastFile + "]";
                    saveToolStripMenuItem.Enabled = true;
                }
            }
        }

        Point MouseDownLocation;

        DataInputCollection _Inputs = new DataInputCollection();
        List<ObjectCache> _VariableCache = new List<ObjectCache>();
        DataVariableCollection _Vars = new DataVariableCollection();
        BindingList<UCTopologyItem> _List = new BindingList<UCTopologyItem>();
        List<ConnectedLine> _Lines = new List<ConnectedLine>();

        bool _InPlay = false;
        ConnectedLine _Current = new ConnectedLine();

        public FEditTopology() { InitializeComponent(); }
        public FEditTopology(string defaultFile) : this()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);

            ITopologyItem.OnException += ITopologyItem_OnException;

            BindingSource bs = new BindingSource();
            bs.DataSource = _List;
            cmItems.DataSource = _List;
            cmItems.DisplayMember = "Title";
            cmItems.ValueMember = "Title";
            LastFile = "";

            LoadActions(Assembly.GetAssembly(typeof(EmptyInput)));
            LoadActions(Assembly.GetAssembly(typeof(ScriptProcess)));

            ToolStripItemComparer.SortToolStripItemCollection(inputToolStripMenuItem.DropDownItems);
            ToolStripItemComparer.SortToolStripItemCollection(processToolStripMenuItem.DropDownItems);

            if (!string.IsNullOrEmpty(defaultFile))
                LoadFile(defaultFile);
        }
        void ITopologyItem_OnException(ITopologyItem sender, Exception e)
        {
            if (InvokeRequired)
            {
                Invoke(new ITopologyItem.delOnException(ITopologyItem_OnException), sender, e);
                return;
            }

            if (_InPlay)
            {
                // Stop-it
                playToolStripMenuItem_Click(null, null);
            }

            rError.Text = e.ToString();
            pError.Visible = true;
        }
        public void LoadActions(Assembly asm)
        {
            Type tin = typeof(IDataInput);
            Type tpr = typeof(IDataProcess);
            Type ti = typeof(ITopologyItem);

            foreach (Type t in asm.GetTypes())
            {
                if (t == tin || t == tpr) continue;
                if (!t.IsPublic) continue;
                if (!ReflectionHelper.HavePublicConstructor(t)) continue;

                if (!ti.IsAssignableFrom(t)) continue;

                using (ITopologyItem d = (ITopologyItem)Activator.CreateInstance(t))
                {
                    if (tin.IsAssignableFrom(t)) AddItem(d, inputToolStripMenuItem);
                    else if (tpr.IsAssignableFrom(t)) AddItem(d, processToolStripMenuItem);
                }
            }
        }
        void AddItem(ITopologyItem n, ToolStripMenuItem parent)
        {
            ToolStripMenuItem m = new ToolStripMenuItem();
            m.Text = n.Title;
            m.ForeColor = n.DesignForeColor;
            m.BackColor = n.DesignBackColor;

            m.Tag = n.GetType();
            parent.DropDownItems.Add(m);
            m.Click += M_Click;
        }
        void M_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem t = (ToolStripMenuItem)sender;
            ITopologyItem n = (ITopologyItem)Activator.CreateInstance((Type)t.Tag);
            CreateItem(n, Point.Empty);
        }
        void Select(UCTopologyItem top)
        {
            if (top == null)
            {
                if (cmItems.SelectedItem != null && cmItems.SelectedItem is UCTopologyItem)
                {
                    ((UCTopologyItem)cmItems.SelectedItem).Selected = false;
                }

                propertyGrid1.SelectedObject = _Vars.Designer;
                propertyGrid1.ExpandAllGridItems();
                cmItems.SelectedItem = null;
                _Current = new ConnectedLine();
                pItems.Invalidate();
            }
            else
            {
                cmItems.SelectedItem = top;
                propertyGrid1.SelectedObject = top.Item;
                propertyGrid1.ExpandAllGridItems();
                top.Selected = true;
            }

            propertyGrid1.Update();
        }
        void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            UCTopologyItem top = (UCTopologyItem)sender;

            MouseDownLocation = e.Location;
            Select(top);

            switch (e.Button)
            {
                default:
                    {
                        if (_Current.From != null)
                        {
                            goto case MouseButtons.Right;
                        }
                        break;
                    }
                case MouseButtons.Right:
                    {
                        if (_Current.From == null)
                        {
                            _Current.From = top;
                            pItems.Invalidate();
                        }
                        else
                        {
                            if (_Current.IsAllowed(top))
                            {
                                if (_Current.Apply(top))
                                {
                                    _Lines.Add(_Current);
                                    _Current = new ConnectedLine();
                                    pItems.Invalidate();
                                }
                                else
                                {
                                    _Current.To = null;
                                    _Current.From = null;
                                    pItems.Invalidate();
                                }
                            }
                            else
                            {
                                _Current.To = null;
                                _Current.From = null;
                                pItems.Invalidate();
                            }
                        }
                        break;
                    }
            }
        }
        void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Control c = (Control)sender;
                c.Left = e.X + c.Left - MouseDownLocation.X;
                c.Top = e.Y + c.Top - MouseDownLocation.Y;

                if (_Lines.Count > 0) pItems.Invalidate();
            }
        }
        void cmItems_Format(object sender, ListControlConvertEventArgs e)
        {
            UCTopologyItem top = (UCTopologyItem)e.ListItem;
            e.Value = top.Title;
        }
        void propertyGrid1_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            if (propertyGrid1.SelectedObject is ITopologyItem)
            {
                ITopologyItem t = (ITopologyItem)propertyGrid1.SelectedObject;

                pItems.Invalidate(true);

                UCTopologyItem c = SearchControl(t);
                if (c != null) c.RefreshDesign();

                for (int x = 0; x < _List.Count; x++)
                    _List.ResetItem(x);
            }
        }
        void cmItems_SelectedIndexChanged(object sender, EventArgs e)
        {
            UCTopologyItem top = (UCTopologyItem)cmItems.SelectedItem;
            Select(top);
        }
        void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }
        void pItems_Paint(object sender, PaintEventArgs e)
        {
            Point from;
            Point to;

            if (_Current.From != null)
            {
                _Current.GetPointFromDraw(pItems, out from, out to);

                bool inUse = _InPlay && !_Current.From.AreInUse.InUse;

                using (Pen pen = new Pen(inUse ? Color.FromArgb(160, _Current.From.BackColor) : _Current.From.BackColor, 10F))
                //using (Pen pen = new Pen(_Current.From.BackColor, 10F))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.ArrowAnchor;

                    if (inUse)
                        pen.DashStyle = DashStyle.Dot;

                    e.Graphics.DrawLine(pen, from, to);
                }
            }

            foreach (ConnectedLine c in _Lines)
            {
                c.GetPointFromDraw(pItems, out from, out to);

                bool inUse = _InPlay && !c.AreInUse.InUse;
                using (Pen pen = new Pen(inUse ? Color.FromArgb(160, c.From.BackColor) : c.From.BackColor, 10F))
                {
                    pen.StartCap = LineCap.RoundAnchor;
                    pen.EndCap = LineCap.ArrowAnchor;

                    if (inUse)
                        pen.DashStyle = DashStyle.Dot;

                    e.Graphics.DrawLine(pen, from, to);
                }
            }
        }
        void pItems_MouseMove(object sender, MouseEventArgs e)
        {
            if (_Current != null && _Current.From != null)
                pItems.Invalidate();
        }
        void FEditTopology_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    {
                        if (_Current.From != null)
                        {
                            _Current.From = null;
                            _Current.To = null;
                            pItems.Invalidate();

                            e.Handled = true;
                            e.SuppressKeyPress = true;
                        }
                        break;
                    }
                case Keys.Delete:
                    {
                        if (ActiveControl == propertyGrid1) return;
                        if (_Current.From != null)
                            goto case Keys.Escape;

                        UCTopologyItem uc = (UCTopologyItem)cmItems.SelectedItem;
                        if (uc != null)
                        {
                            bool entra = false;

                            for (int x = _Lines.Count - 1; x >= 0; x--)
                            {
                                ConnectedLine c = _Lines[x];
                                if (c.From == uc || c.To == uc)
                                {
                                    _Lines.Remove(c);
                                    c.FromItem.Process.Remove(c.ToItem);
                                    entra = true;
                                }
                            }

                            if (!entra)
                            {
                                Delete(uc);

                                foreach (UCTopologyItem c in pItems.Controls)
                                {
                                    Select(c);
                                    break;
                                }
                            }

                            pItems.Invalidate();
                            propertyGrid1.Update();

                            e.Handled = true;
                            e.SuppressKeyPress = true;
                        }
                        break;
                    }
            }
        }
        void Delete(UCTopologyItem uc)
        {
            if (uc.Item is IDataInput)
            {
                IDataInput i = (IDataInput)uc.Item;
                _Inputs.Remove(i);

                if (i.RaiseMode != null) i.RaiseMode.Stop(i);
            }

            uc.Item.OnProcess -= Item_OnProcess;

            _List.Remove(uc);
            uc.Parent.Controls.Remove(uc);
            uc.Dispose();
        }
        TLYFile GetSaveFile()
        {
            TLYFile t = new TLYFile();
            t.Variables = _Vars.ToArray();

            int id = 0;
            foreach (UCTopologyItem u in pItems.Controls)
            {
                u.Item.Id = id;
                id++;

                t.Items.Add(u.Item.Id, new TLYFile.TopologyItem() { Item = u.Item, Position = u.Location });
            }

            foreach (ConnectedLine line in _Lines)
                t.Relations.Add(new TLYFile.Relation() { From = line.FromItem.Id, To = line.ToItem.Id });

            return t;
        }
        void Save(string file)
        {
            TLYFile t = GetSaveFile();
            LasterHelper.SetEnvironmentPath(file);

            t.Save(file);
            LastFile = file;
        }
        void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Save(LastFile);
        }
        void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sv = new SaveFileDialog()
            {
                Title = "Topology Files",
                Filter = "Topology Files|*.tly"
            })
            {
                if (!string.IsNullOrEmpty(_LastFile))
                {
                    sv.InitialDirectory = Path.GetDirectoryName(_LastFile);
                }
                if (sv.ShowDialog() != DialogResult.OK) return;

                Save(sv.FileName);
            }
        }
        void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog sv = new OpenFileDialog()
            {
                Title = "Topology Files",
                Filter = "Topology Files|*.tly"
            })
            {
                if (!string.IsNullOrEmpty(_LastFile))
                {
                    sv.InitialDirectory = Path.GetDirectoryName(_LastFile);
                }
                if (sv.ShowDialog() != DialogResult.OK) return;

                LoadFile(sv.FileName);
            }
        }
        void LoadFile(string fileName)
        {
            TLYFile t = null;

            bool isFile = false;

            try
            {
                if (File.Exists(fileName))
                {
                    t = TLYFile.LoadFromFile(fileName);
                    isFile = true;
                }
                else t = TLYFile.Load(fileName);
            }
            catch(Exception e)
            {
                ITopologyItem_OnException(null, e);
                return;
            }
            if (t != null)
            {
                NewTopology();

                if (isFile)
                {
                    LasterHelper.SetEnvironmentPath(fileName);
                    LastFile = fileName;
                }
                else
                {
                    LasterHelper.SetEnvironmentPath("");
                    LastFile = "";
                }
                if (t.Variables != null)
                {
                    foreach (Variable v in t.Variables)
                        _Vars.Add(v.Clone());
                }

                if (t.Items.Values != null)
                {
                    foreach (TLYFile.TopologyItem i in t.Items.Values)
                    {
                        CreateItem(i.Item, i.Position);
                    }

                    if (t.Relations != null)
                    {
                        foreach (TLYFile.Relation rel in t.Relations)
                        {
                            TLYFile.TopologyItem from, to;
                            if (t.Items.TryGetValue(rel.From, out from) && t.Items.TryGetValue(rel.To, out to) && from != null && to != null)
                            {
                                UCTopologyItem searchFrom = SearchControl(from.Item);
                                UCTopologyItem searchTo = SearchControl(to.Item);

                                if (searchFrom != null && searchTo != null)
                                {
                                    _Lines.Add(new ConnectedLine() { From = searchFrom, To = searchTo });

                                    if (to.Item is IDataProcess)
                                        from.Item.Process.Add((IDataProcess)to.Item);
                                }
                            }
                        }

                    }
                }

                pItems.Invalidate();
            }
        }

        void NewTopology()
        {
            if (_InPlay)
            {
                // Stop-it
                playToolStripMenuItem_Click(null, null);
            }

            LastFile = "";

            _Lines.Clear();
            _Current = new ConnectedLine();
            Select(null);
            _Vars.Clear();
            _VariableCache.Clear();
            generateExeToolStripMenuItem.Enabled = false;
            LasterHelper.SetEnvironmentPath(null);

            foreach (UCTopologyItem u in _List.ToArray()) Delete(u);
        }
        UCTopologyItem SearchControl(ITopologyItem item)
        {
            if (item == null) return null;

            if (item.Tag != null && item.Tag is UCTopologyItem)
                return (UCTopologyItem)item.Tag;

            foreach (UCTopologyItem c in pItems.Controls)
                if (c.Item == item) return c;
            return null;
        }
        void CreateItem(ITopologyItem n, Point location)
        {
            if (n == null) return;

            UCTopologyItem top = new UCTopologyItem(n);
            top.Location = location;
            n.Tag = top;
            top.RefreshInPlay(_InPlay);

            top.MouseDown += pictureBox1_MouseDown;
            top.MouseMove += pictureBox1_MouseMove;
            pItems.Controls.Add(top);

            n.OnProcess += Item_OnProcess;
            _List.Add(top);

            if (n is IDataInput)
            {
                IDataInput i = (IDataInput)n;
                _Inputs.Add(i);

                if (_InPlay)
                {
                    _Inputs.Stop();
                    _Inputs.Start();
                }
            }

            Select(top);
        }
        void pItems_MouseDown(object sender, MouseEventArgs e)
        {
            Select(null);
        }
        void playToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new EventHandler(playToolStripMenuItem_Click), sender, e);
                return;
            }

            if (!_InPlay && _Inputs.Count <= 0)
                return;

            _InPlay = !_InPlay;

            foreach (UCTopologyItem c in pItems.Controls)
                c.RefreshInPlay(_InPlay);

            playToolStripMenuItem.Visible = playToolStripMenuItem.Enabled = !_InPlay;
            stopToolStripMenuItem.Visible = stopToolStripMenuItem.Enabled = _InPlay;

            if (!_InPlay)
            {
                _Inputs.Stop();
                tPaintPlay.Enabled = false;

                // Restore variable cache
                foreach (ObjectCache v in _VariableCache) v.Restore();
                _VariableCache.Clear();
                propertyGrid1.Refresh();
            }
            else
            {
                _VariableCache.Clear();
                try
                {
                    TLYFile.RemplaceVariables(_Inputs, _Vars.ToArray(), _VariableCache);
                }
                catch (Exception ex)
                {
                    ITopologyItem_OnException(null, ex);
                    return;
                }

                pError.Visible = false;
                tPaintPlay.Interval = AreInUse.InUseMillisecons / 2;
                tPaintPlay.Enabled = true;

                foreach (ConnectedLine l in _Lines) l.AreInUse.Clear();

                Thread th = new Thread(new ThreadStart(StartAsync))
                {
                    IsBackground = true,
                };
                th.SetApartmentState(ApartmentState.STA);
                th.Start();
            }

            pItems.Invalidate(true);
        }
        void StartAsync()
        {
            if (!_Inputs.Start() && _InPlay)
            {
                playToolStripMenuItem_Click(null, null);
            }
        }
        void Item_OnProcess(ITopologyItem sender, EProcessState state)
        {
            if (sender == null) return;

            UCTopologyItem uc = (UCTopologyItem)sender.Tag;
            if (uc != null)
            {
                // Activamos el procesado
                uc.AreInUse.InUse = state == EProcessState.PreProcess;
                uc.Invalidate();
            }

            // Controlamos las lineas de conexión
            foreach (ConnectedLine l in _Lines)
            {
                if (state == EProcessState.PreProcess)
                {
                    // Desactivar todas las lineas que iban a él
                    if (l.ToItem == sender) l.AreInUse.InUse = false;
                }
                else
                {
                    // Activar todas las lineas que salen de él
                    if (l.FromItem == sender) l.AreInUse.InUse = true;
                }
            }

            pItems.Invalidate(false);
        }
        void tPaintPlay_Tick(object sender, EventArgs e)
        {
            foreach (ConnectedLine l in _Lines)
                if (l.AreInUse.AreChanged)
                {
                    pItems.Invalidate(true);
                    return;
                }
            foreach (UCTopologyItem c in pItems.Controls)
                if (c.AreInUse.AreChanged)
                {
                    pItems.Invalidate(true);
                    return;
                }
        }
        void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NewTopology();
        }
        void rError_DoubleClick(object sender, EventArgs e)
        {
            pError.Visible = false;
        }
        void generateExeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sv = new SaveFileDialog())
            {
                sv.Filter = "Exe file|*.exe";
                sv.DefaultExt = "exe";

                if (sv.ShowDialog() != DialogResult.OK) return;

                string pwd = FCreatePassword.ShowForm();
                if (string.IsNullOrEmpty(pwd)) return;

                // Copiar librerias
                if (Path.GetDirectoryName(Application.ExecutablePath) != Path.GetDirectoryName(sv.FileName))
                {
                    if (MessageBox.Show("Do you want to copy dll files to?", "Library files", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                    {
                        foreach (string file in Directory.GetFiles(Path.GetDirectoryName(Application.ExecutablePath), "*.dll", SearchOption.TopDirectoryOnly))
                        {
                            string dest = Path.Combine(Path.GetDirectoryName(sv.FileName), Path.GetFileName(file));
                            if (dest == file) continue;

                            if (File.Exists(dest))
                                File.Delete(dest);
                            File.Copy(file, dest);
                        }
                    }
                }

                byte[] hash = Encoding.UTF8.GetBytes(pwd);
                hash = HashHelper.HashRaw(HashHelper.EHashType.Sha512, hash, 0, hash.Length);

                TLYFile t = GetSaveFile();
                PacketHeader header = new PacketHeader()
                {
                    H = hash,
                    D = Encoding.UTF8.GetBytes(t.Save())
                };

                header.Encrypt(true);

                // Leer exe original
                byte[] ar = File.ReadAllBytes(Application.ExecutablePath);

                using (FileStream fs = new FileStream(sv.FileName, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    // Escribir exe
                    fs.Write(ar, 0, ar.Length);

                    // Escribir contenido comprimido, en UTF8
                    ar = Encoding.UTF8.GetBytes(SerializationHelper.Serialize(header, SerializationHelper.EFormat.Json));
                    ar = CompressHelper.Compress(ar, 0, ar.Length, true);
                    fs.Write(ar, 0, ar.Length);

                    // Grabar tamaño añadido
                    ar = BitConverter.GetBytes(ar.Length);
                    fs.Write(ar, 0, ar.Length);

                    // Grabar palabra clave de fin de archivo
                    ar = Encoding.ASCII.GetBytes("PACK");
                    fs.Write(ar, 0, ar.Length);
                }

                Pr.Process.Start(Path.GetDirectoryName(sv.FileName));
            }
        }
        void pItems_ControlAdded(object sender, ControlEventArgs e)
        {
            generateExeToolStripMenuItem.Enabled = pItems.Controls.Count > 0;
        }
    }
}