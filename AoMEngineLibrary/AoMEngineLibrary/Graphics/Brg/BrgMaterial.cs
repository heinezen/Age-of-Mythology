﻿namespace AoMEngineLibrary.Graphics.Brg
{
    using AoMEngineLibrary.Graphics.Model;
    using MiscUtil.Conversion;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Text;

    public class BrgMaterial : Material
    {
        public BrgFile ParentFile;
        public string EditorName
        {
            get
            {
                return "Mat ID: " + id;
            }
        }

        public int id;
        public BrgMatFlag Flags { get; set; }
        public int unknown01b;
        public string DiffuseMap { get; set; }
        public string BumpMap { get; set; }
        public List<BrgMatSFX> sfx;

        public BrgMaterial(BrgBinaryReader reader, BrgFile file)
            : base()
        {
            ParentFile = file;

            id = reader.ReadInt32();
            Flags = (BrgMatFlag)reader.ReadInt32();
            unknown01b = reader.ReadInt32();
            int nameLength = reader.ReadInt32();
            this.DiffuseColor = reader.ReadColor3D();
            this.AmbientColor = reader.ReadColor3D();
            this.SpecularColor = reader.ReadColor3D();
            this.EmissiveColor = reader.ReadColor3D();

            this.DiffuseMap = reader.ReadString(nameLength);
            if (Flags.HasFlag(BrgMatFlag.SpecularExponent))
            {
                this.SpecularExponent = reader.ReadSingle();
            }
            if (Flags.HasFlag(BrgMatFlag.BumpMap))
            {
                this.BumpMap = reader.ReadString(reader.ReadInt32());
            }
            if (Flags.HasFlag(BrgMatFlag.Alpha))
            {
                this.Opacity = reader.ReadSingle();
            }

            if (Flags.HasFlag(BrgMatFlag.REFLECTIONTEXTURE))
            {
                byte numSFX = reader.ReadByte();
                sfx = new List<BrgMatSFX>(numSFX);
                for (int i = 0; i < numSFX; i++)
                {
                    sfx.Add(reader.ReadMaterialSFX());
                }
            }
            else
            {
                sfx = new List<BrgMatSFX>();
            }
        }
        public BrgMaterial(BrgFile file)
            : base()
        {
            this.ParentFile = file;
            this.id = 0;
            this.Flags = 0;
            this.unknown01b = 0;

            this.DiffuseMap = string.Empty;
            this.BumpMap = string.Empty;

            this.sfx = new List<BrgMatSFX>();
        }

        public void Write(BrgBinaryWriter writer)
        {
            writer.Write(this.id);
            writer.Write((int)this.Flags);

            writer.Write(this.unknown01b);
            writer.Write(Encoding.UTF8.GetByteCount(this.DiffuseMap));

            writer.WriteColor3D(this.DiffuseColor);
            writer.WriteColor3D(this.AmbientColor);
            writer.WriteColor3D(this.SpecularColor);
            writer.WriteColor3D(this.EmissiveColor);

            writer.WriteString(this.DiffuseMap, 0);

            if (this.Flags.HasFlag(BrgMatFlag.SpecularExponent))
            {
                writer.Write(this.SpecularExponent);
            }
            if (this.Flags.HasFlag(BrgMatFlag.BumpMap))
            {
                writer.WriteString(this.BumpMap, 4);
            }
            if (this.Flags.HasFlag(BrgMatFlag.Alpha))
            {
                writer.Write(this.Opacity);
            }

            if (this.Flags.HasFlag(BrgMatFlag.REFLECTIONTEXTURE))
            {
                writer.Write((byte)this.sfx.Count);
                for (int i = 0; i < this.sfx.Count; i++)
                {
                    writer.Write(this.sfx[i].Id);
                    writer.WriteString(this.sfx[i].Name, 2);
                }
            }
        }

        public void WriteExternal(FileStream fileStream)
        {
            using (BrgBinaryWriter writer = new BrgBinaryWriter(new LittleEndianBitConverter(), fileStream))
            {
                writer.Write(1280463949); // MTRL
                writer.Write(Encoding.UTF8.GetByteCount(this.DiffuseMap));

                writer.Write(new byte[20]);

                writer.WriteColor3D(this.DiffuseColor);
                writer.WriteColor3D(this.AmbientColor);
                writer.WriteColor3D(this.SpecularColor);
                writer.WriteColor3D(this.EmissiveColor);
                writer.Write(this.SpecularExponent);
                writer.Write(this.Opacity);

                writer.Write(-1);
                writer.Write(16777216);
                writer.Write(65793);
                if (this.Flags.HasFlag(BrgMatFlag.AdditiveBlend))
                {
                    writer.Write(10);
                }
                else
                {
                    writer.Write(6);
                }
                writer.Write(new byte[16]);

                if (Flags.HasFlag(BrgMatFlag.PixelXForm1))
                {
                    writer.Write(4);
                }
                else
                {
                    writer.Write(0);
                }

                writer.Write(0);

                if (Flags.HasFlag(BrgMatFlag.REFLECTIONTEXTURE))
                {
                    writer.Write(1275068416);
                    writer.Write(12);
                    writer.Write(0);
                    writer.Write(1);
                }
                else
                {
                    writer.Write(new byte[16]);
                }

                writer.Write(new byte[32]);
                writer.Write(-1);
                writer.Write(-1);
                writer.Write(-1);
                writer.Write(-1);
                writer.Write(-1);
                writer.Write(-1);
                writer.Write(new byte[16]);

                writer.WriteString(DiffuseMap);
            }
        }
    }
}
