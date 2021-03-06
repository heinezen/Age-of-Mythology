﻿namespace AoMEngineLibrary.Graphics.Prt
{
    using System;

    public class PrtColorStage
    {
        public bool UsePalette { get; set; }
        public Texel Color { get; set; }
        public float Hold { get; set; }
        public float Fade { get; set; }

        private PrtColorStage()
        {

        }
        public PrtColorStage(PrtBinaryReader reader)
        {
            this.UsePalette = reader.ReadBoolean();
            reader.ReadBytes(3);

            this.Color = reader.ReadTexel();
            this.Hold = reader.ReadSingle();
            this.Fade = reader.ReadSingle();
        }

        public void Write(PrtBinaryWriter writer)
        {
            writer.Write(this.UsePalette);
            writer.Write(new byte[3]);

            writer.WriteTexel(this.Color);
            writer.Write(this.Hold);
            writer.Write(this.Fade);
        }
    }
}
