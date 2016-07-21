﻿using Laster.Core.Helpers;
using Laster.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Laster.Inputs.File
{
    public class OpenFileInput : IDataInput
    {
        public enum EMultipleType
        {
            Simple,
            MultipleAsArray,
            MultipleAsEnumerator
        }

        /// <summary>
        /// Constructor por defecto
        /// </summary>
        public OpenFileInput() : base() { }

        public EMultipleType Multiselect { get; set; }
        public string InitialDirectory { get; set; }
        public string Filter { get; set; }
        public int FilterIndex { get; set; }
        public string DefaultExt { get; set; }
        [Description("Require Filter")]
        public bool SearchInProgramArguments { get; set; }

        public override string Title { get { return "OpenFile"; } }

        protected override IData OnGetData()
        {
            List<string> files = new List<string>();
            if (SearchInProgramArguments && !string.IsNullOrEmpty(Filter))
            {
                // Sacar el archivo de los argumentos del programa
                int x = 0;
                string[] args = Environment.GetCommandLineArgs();
                foreach (string f in Filter.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    x++;
                    if (x % 2 != 0) continue;

                    foreach (string a in args)
                        foreach (string ext in f.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (StringHelper.LikeString(a, ext))
                            {
                                files.Add(a);
                                break;
                            }
                        }
                }

            }

            if (files.Count == 0)
            {
                // Dialogo
                Task<string[]> t = TaskHelper.StartSTATask<string[]>(() => { return GetFilesByDialog(); });
                t.Wait();

                if (t.Result != null) files.AddRange(t.Result);
            }

            if (files.Count > 0)
            {
                if (files.Count == 1) return DataObject(files[0]);
                else
                {
                    switch (Multiselect)
                    {
                        case EMultipleType.MultipleAsArray: return DataArray(files);
                        case EMultipleType.MultipleAsEnumerator: return DataEnumerable(files);
                        default: return DataObject(files[0]);
                    }
                }
            }

            return DataEmpty();
        }
        string[] GetFilesByDialog()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.InitialDirectory = Environment.ExpandEnvironmentVariables(InitialDirectory);
                dialog.Title = Title;
                dialog.Filter = Filter;
                dialog.DefaultExt = DefaultExt;
                dialog.FilterIndex = FilterIndex;
                dialog.CheckFileExists = true;
                dialog.RestoreDirectory = true;
                dialog.AutoUpgradeEnabled = true;
                dialog.Multiselect = Multiselect != EMultipleType.Simple;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    return dialog.FileNames;
                }
            }
            return null;
        }
    }
}