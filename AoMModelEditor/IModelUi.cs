﻿namespace AoMModelEditor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using AoMEngineLibrary.Graphics.Model;

    public interface IModelUI
    {
        MainForm Plugin { get; set; }
        string FileName { get; set; }
        int FilterIndex { get; }

        void Read(System.IO.FileStream stream);
        void Write(System.IO.FileStream stream);
        void Clear();

        void LoadUI();
        void SaveUI();

        void Import(string fileName);
        void Export(string fileName);
    }
}
