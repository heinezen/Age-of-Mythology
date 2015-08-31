﻿namespace AoMEngineLibrary.AMP
{
    using AoMEngineLibrary.Graphics;
    using AoMEngineLibrary.Graphics.Grn;
    using AoMEngineLibrary.Graphics.Model;
    using Autodesk.Max;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    public class GrnMax : IModelMaxUI
    {
        public GrnFile File { get; set; }
        public MaxPluginForm Plugin { get; set; }
        public string FileName { get; set; }
        public int FilterIndex { get { return 2; } }

        private Dictionary<string, int> boneMap;
        private bool matGroupInit;

        private GrnExportSetting ExportSetting { get; set; }

        public GrnMax(MaxPluginForm plugin)
        {
            this.File = new GrnFile();
            this.FileName = "Untitled";
            this.Plugin = plugin;
            this.boneMap = new Dictionary<string, int>();
            this.matGroupInit = false;
            this.ExportSetting = GrnExportSetting.Model;
        }

        #region Setup
        public void Read(FileStream stream)
        {
            this.File = new GrnFile();
            this.File.Read(stream);
            this.FileName = stream.Name;
        }
        public void Write(FileStream stream)
        {
            this.File.Write(stream);
            this.FileName = stream.Name;
        }
        public void Clear()
        {
            this.File = new GrnFile();
            this.FileName = Path.GetDirectoryName(this.FileName) + "\\Untitled";
            this.boneMap = new Dictionary<string, int>();
            this.matGroupInit = false;
        }
        #endregion

        #region Import/Export
        public void Import()
        {
            Maxscript.Command("importStartTime = timeStamp()");
            string mainObject = "mainObject";
            string boneArray = "boneArray";

            if (this.File.Meshes.Count > 0 || Maxscript.QueryBoolean("boneArray == undefined or not(isvalidnode boneArray[1])"))
            {
                this.boneMap = new Dictionary<string, int>();
            }
            //if (!this.matGroupInit)
            if (this.File.Materials.Count > 0)
            {
                Maxscript.Command("matGroup = multimaterial numsubs:{0}", this.File.Materials.Count);
                this.matGroupInit = true;
            }

            //this.Plugin.ProgDialog.SetProgressText("Importing skeleton...");
            this.ImportSkeleton(boneArray);
            //this.Plugin.ProgDialog.SetProgressValue(35);

            //this.Plugin.ProgDialog.SetProgressText("Importing meshes...");
            for (int i = 0; i < this.File.Meshes.Count; i++)
            {
                this.ImportMesh(this.File.Meshes[i], mainObject, boneArray);
                Maxscript.Command("{0}.material = matGroup", mainObject);
            }
            //this.Plugin.ProgDialog.SetProgressValue(70);

            //this.Plugin.ProgDialog.SetProgressText("Importing animation...");
            if (this.File.Animation.Duration > 0)
            {
                this.ImportAnimation(boneArray);
            }
            //this.Plugin.ProgDialog.SetProgressValue(85);

            //this.Plugin.ProgDialog.SetProgressText("Importing materials...");
            for (int i = 0; i < this.File.Materials.Count; i++)
            {
                Maxscript.Command("matGroup[{0}] = {1}", i + 1, ImportMaterial(this.File.Materials[i]));
            }
            //this.Plugin.ProgDialog.SetProgressValue(100);

            Maxscript.Command("max zoomext sel all");
            Maxscript.Command("importEndTime = timeStamp()");
            Maxscript.Format("Import took % seconds\n", "((importEndTime - importStartTime) / 1000.0)");
            //if (this.Plugin.ProgDialog.InvokeRequired)
            //{
            //    this.Plugin.ProgDialog.BeginInvoke(new Action(() => this.Plugin.ProgDialog.Close()));
            //}
        }
        private void ImportSkeleton(string boneArray)
        {
            if (this.boneMap.Count == 0)
            {
                Maxscript.NewArray(boneArray);
            }

            for (int i = 0; i < this.File.Bones.Count; ++i)
            {
                GrnBone bone = this.File.Bones[i];
                if (bone.Name == "__Root")
                {
                    continue;
                }

                if (this.boneMap.ContainsKey(bone.Name))
                {
                    Maxscript.Command("{0}[{1}].transform = {2}", boneArray, this.boneMap[bone.Name] + 1,
                        this.GetBoneLocalTransform(bone, "boneTransMat"));
                }
                else
                {
                    this.boneMap.Add(bone.Name, this.boneMap.Count);
                    Maxscript.Append(boneArray, this.CreateBone(bone));
                }

                if (bone.ParentIndex > 0)
                {
                    Maxscript.Command("{0}[{1}].parent = {0}[{2}]", boneArray, this.boneMap[bone.Name] + 1,
                        this.boneMap[this.File.Bones[bone.ParentIndex].Name] + 1);
                    Maxscript.Command("{0}[{1}].transform *= {0}[{1}].parent.transform", boneArray, this.boneMap[bone.Name] + 1);
                }
            }
        }
        private void ImportMesh(GrnMesh mesh, string mainObject, string boneArray)
        {
            string vertArray = "";
            string texVerts = "";
            string faceMats = "";
            string faceArray = "";
            string tFaceArray = "";
            vertArray = Maxscript.NewArray("vertArray");
            texVerts = Maxscript.NewArray("texVerts");
            faceMats = Maxscript.NewArray("faceMats");
            faceArray = Maxscript.NewArray("faceArray");
            tFaceArray = Maxscript.NewArray("tFaceArray");

            for (int i = 0; i < mesh.Vertices.Count; ++i)
            {
                Maxscript.Append(vertArray, Maxscript.Point3Literal(mesh.Vertices[i]));
            }

            for (int i = 0; i < mesh.TextureCoordinates.Count; ++i)
            {
                Maxscript.Append(texVerts, Maxscript.Point3Literal(mesh.TextureCoordinates[i]));
            }

            foreach (var face in mesh.Faces)
            {
                Maxscript.Append(faceMats, face.MaterialIndex + 1);
                Maxscript.Append(faceArray, Maxscript.Point3Literal(face.Indices[0] + 1, face.Indices[1] + 1, face.Indices[2] + 1));
                Maxscript.Append(tFaceArray, Maxscript.Point3Literal(face.TextureIndices[0] + 1, face.TextureIndices[1] + 1, face.TextureIndices[2] + 1));
            }

            Maxscript.Command("meshBone = getNodeByName \"{0}\"", mesh.Name);
            Maxscript.Command("{5} = mesh name:\"{0}\" vertices:{1} faces:{2} materialIDs:{3} tverts:{4}", mesh.Name, vertArray, faceArray, faceMats, texVerts, mainObject);
            if (Maxscript.QueryBoolean("meshBone != undefined"))
            {
                Maxscript.Command("{0}.parent = meshBone.parent", mainObject);
                Maxscript.Command("delete meshBone");
            }

            Maxscript.CommentTitle("TVert Hack"); // Needed <= 3ds Max 2014; idk about 2015+
            Maxscript.Command("buildTVFaces {0}", mainObject);
            for (int i = 0; i < mesh.Faces.Count; ++i)
            {
                Maxscript.Command("setTVFace {0} {1} {2}[{1}]", mainObject, i + 1, tFaceArray);
            }

            Maxscript.Command("max modify mode");
            Maxscript.Command("select {0}", mainObject);
            Maxscript.Command("addModifier {0} (Edit_Normals()) ui:off", mainObject);
            Maxscript.Command("modPanel.setCurrentObject {0}.modifiers[#edit_normals]", mainObject);

            Maxscript.Command("{0}.modifiers[#edit_normals].Break selection:#{{1..{1}}}", mainObject, mesh.Normals.Count);
            Maxscript.Command("meshSetNormalIdFunc = {0}.modifiers[#edit_normals].SetNormalID", mainObject);
            for (int i = 0; i < mesh.Faces.Count; ++i)
            {
                Maxscript.Command("meshSetNormalIdFunc {0} {1} {2}",
                    i + 1, 1, mesh.Faces[i].NormalIndices[0] + 1);
                Maxscript.Command("meshSetNormalIdFunc {0} {1} {2}",
                    i + 1, 2, mesh.Faces[i].NormalIndices[1] + 1);
                Maxscript.Command("meshSetNormalIdFunc {0} {1} {2}",
                    i + 1, 3, mesh.Faces[i].NormalIndices[2] + 1);
            }
            Maxscript.Command("{0}.modifiers[#edit_normals].MakeExplicit selection:#{{1..{1}}}", mainObject, mesh.Normals.Count);
            Maxscript.Command("meshSetNormalFunc = {0}.modifiers[#edit_normals].SetNormal", mainObject);
            for (int i = 0; i < mesh.Normals.Count; i++)
            {
                Maxscript.Command("meshSetNormalFunc {0} {1}", i + 1, Maxscript.Point3Literal(mesh.Normals[i]));
            }
            Maxscript.Command("collapseStack {0}", mainObject);

            // Bones
            Maxscript.Command("skinMod = Skin()");
            Maxscript.Command("addModifier {0} skinMod", mainObject);
            Maxscript.Command("modPanel.setCurrentObject skinMod");
            Maxscript.Command("skinAddBoneFunc = skinOps.addBone");
            for (int i = 0; i < mesh.BoneBindings.Count; ++i)
            {
                GrnBoneBinding boneBinding = mesh.BoneBindings[i];
                string boundingScale = Maxscript.NewPoint3<float>("boundingScale",
                    (boneBinding.OBBMax.X - boneBinding.OBBMin.X), (boneBinding.OBBMax.Y - boneBinding.OBBMin.Y), (boneBinding.OBBMax.Z - boneBinding.OBBMin.Z));
                Maxscript.Command("{0}[{1}].boxsize = {2}", boneArray, this.boneMap[this.File.Bones[boneBinding.BoneIndex].Name] + 1, boundingScale);
                Maxscript.Command("skinAddBoneFunc skinMod {0}[{1}] {2}", boneArray,
                    this.boneMap[this.File.Bones[boneBinding.BoneIndex].Name] + 1, i + 1 == mesh.BoneBindings.Count ? 1 : 0);
            }
            Maxscript.Command("completeRedraw()"); // would get "Exceeded the vertex countSkin:skin" error without this
            Maxscript.Command("skinReplaceVertWeightsFunc = skinOps.ReplaceVertexWeights");
            for (int i = 0; i < mesh.VertexWeights.Count; ++i)
            {
                // Index correspond to order that the bones were added to Skin mod
                string boneIndexArray = Maxscript.NewArray("boneIndexArray");
                string weightsArray = Maxscript.NewArray("weightsArray");
                for (int j = 0; j < mesh.VertexWeights[i].Weights.Count; ++j)
                {
                    Maxscript.Append(boneIndexArray, mesh.VertexWeights[i].BoneIndices[j] + 1);
                    Maxscript.Append(weightsArray, mesh.VertexWeights[i].Weights[j]);
                }
                Maxscript.Command("skinReplaceVertWeightsFunc skinMod {0} {1} {2}",
                    i + 1, 1, 0.0);
                Maxscript.Command("skinReplaceVertWeightsFunc skinMod {0} {1} {2}",
                    i + 1, boneIndexArray, weightsArray);
            }
        }
        private void ImportAnimation(string boneArray)
        {
            Maxscript.Command("frameRate = {0}", 30);
            Maxscript.Interval(0, this.File.Animation.Duration);

            for (int i = 0; i < this.File.Animation.BoneTracks.Count; ++i)
            {
                if (this.File.Bones[i].Name == "__Root")
                {
                    continue;
                }
                GrnBoneTrack bone = this.File.Animation.BoneTracks[i];
                // typically bones and bonetracks match up in a file
                // but won't if an anim file is imported on top of a regular model file
                int boneArrayIndex = this.boneMap[this.File.Bones[i].Name] + 1;

                Vector3D pos = new Vector3D();
                Quaternion rot = new Quaternion();
                Matrix3x3 scale = Matrix3x3.Identity;
                HashSet<float> uKeys = new HashSet<float>();
                uKeys.UnionWith(bone.RotationKeys);
                uKeys.UnionWith(bone.ScaleKeys);
                uKeys.UnionWith(bone.PositionKeys);
                List<float> keys = uKeys.ToList();
                keys.Sort();

                for (int j = 0; j < keys.Count; ++j)
                {
                    int index = bone.PositionKeys.IndexOf(keys[j]);
                    if (index >= 0)
                    {
                        pos = bone.Positions[index];
                    }
                    index = bone.RotationKeys.IndexOf(keys[j]);
                    if (index >= 0)
                    {
                        rot = bone.Rotations[index];
                    }
                    index = bone.ScaleKeys.IndexOf(keys[j]);
                    if (index >= 0)
                    {
                        scale = bone.Scales[index];
                    }

                    Maxscript.AnimateAtTime(keys[j], "{0}[{1}][3].controller.value = {2}",
                        boneArray, boneArrayIndex, this.GetBoneLocalTransform("bAnimMatrix", pos, rot, scale));
                }
            }
        }
        private string ImportMaterial(GrnMaterial mat)
        {
            Maxscript.Command("mat = StandardMaterial()");
            Maxscript.Command("mat.name = \"{0}\"", mat.Name);
            Maxscript.Command("mat.adLock = false");
            Maxscript.Command("mat.useSelfIllumColor = true");
            Maxscript.Command("mat.diffuse = color {0} {1} {2}", mat.DiffuseColor.R * 255f, mat.DiffuseColor.G * 255f, mat.DiffuseColor.B * 255f);
            Maxscript.Command("mat.ambient = color {0} {1} {2}", mat.AmbientColor.R * 255f, mat.AmbientColor.G * 255f, mat.AmbientColor.B * 255f);
            Maxscript.Command("mat.specular = color {0} {1} {2}", mat.SpecularColor.R * 255f, mat.SpecularColor.G * 255f, mat.SpecularColor.B * 255f);
            Maxscript.Command("mat.selfIllumColor = color {0} {1} {2}", mat.EmissiveColor.R * 255f, mat.EmissiveColor.G * 255f, mat.EmissiveColor.B * 255f);
            Maxscript.Command("mat.opacity = {0}", mat.Opacity * 100f);
            Maxscript.Command("mat.specularLevel = {0}", mat.SpecularExponent);

            Maxscript.Command("tex = BitmapTexture()");
            Maxscript.Command("tex.name = \"{0}\"", mat.DiffuseTexture.Name);
            Maxscript.Command("tex.filename = \"{0}\"", Path.GetFileName(mat.DiffuseTexture.FileName));
            Maxscript.Command("mat.diffusemap = tex");

            return "mat";
        }
        //public static void ExportAnimToMax()
        //{
        //    Maxscript.Command("frameRate = {0}", Math.Round(1 / AnimFile.Animation.TimeStep));
        //    Maxscript.Interval(0, AnimFile.Animation.Duration);

        //    string boneArray = "boneArray";

        //    // Match up the animation file bones to the model file bones
        //    List<int> boneMap = new List<int>();
        //    int currentNumberOfBones = MeshFile.Bones.Count;
        //    for (int i = 0; i < AnimFile.Bones.Count; ++i)
        //    {
        //        boneMap.Add(-1);
        //        for (int j = 0; j < MeshFile.Bones.Count; ++j)
        //        {
        //            if (AnimFile.Bones[i].Name == MeshFile.Bones[j].Name)
        //            {
        //                boneMap[i] = j;
        //                break;
        //            }
        //        }

        //        if (boneMap[i] == -1)
        //        {
        //            // Create the new bone
        //            GrnBone bone = AnimFile.Bones[i];
        //            boneMap[i] = currentNumberOfBones++;
        //            string bPos = Maxscript.NewPoint3<float>("bPos", bone.Position.X, bone.Position.Y, bone.Position.Z);
        //            Maxscript.Command("bRot = quat {0} {1} {2} {3}", bone.Rotation.X, bone.Rotation.Y, bone.Rotation.Z, bone.Rotation.W);
        //            Maxscript.Command("boneNode = dummy name:\"{0}\" rotation:{1} position:{2} boxsize:{3}",
        //                bone.Name, "bRot", "bPos", "[0.25,0.25,0.25]");
        //            Maxscript.Append(boneArray, "boneNode");
        //        }

        //        //Maxscript.Command("print \"{0} {1}\"", AnimFile.Bones[i].Name, boneMap[i]);
        //    }

        //    // Set the animation file base skeleton pose
        //    for (int i = 0; i < AnimFile.Bones.Count; ++i)
        //    {
        //        GrnBone bone = AnimFile.Bones[i];
        //        string bPos = Maxscript.NewPoint3<float>("bPos", bone.Position.X, bone.Position.Y, bone.Position.Z);
        //        Maxscript.Command("bRot = quat {0} {1} {2} {3}", bone.Rotation.X, bone.Rotation.Y, bone.Rotation.Z, bone.Rotation.W);
        //        Maxscript.Command("{0}[{1}].rotation = {2}", boneArray, boneMap[i] + 1, "bRot");
        //        Maxscript.Command("{0}[{1}].position = {2}", boneArray, boneMap[i] + 1, bPos);

        //        if (AnimFile.Bones[i].ParentIndex != i)
        //        {
        //            Maxscript.Command("{0}[{1}].parent = {0}[{2}]", boneArray, boneMap[i] + 1, boneMap[AnimFile.Bones[i].ParentIndex] + 1);
        //            Maxscript.Command("{0}[{1}].transform *= {0}[{1}].parent.transform", boneArray, boneMap[i] + 1);
        //        }
        //    }

        //    // Animate all bones
        //    for (int i = 0; i < AnimFile.Animation.BoneTracks.Count; ++i)
        //    {
        //        GrnBoneTrack bone = AnimFile.Animation.BoneTracks[i];
        //        for (int j = 0; j < bone.RotationKeys.Count; ++j)
        //        {
        //            Maxscript.Command("bRot = quat {0} {1} {2} {3}", bone.Rotations[j].X, bone.Rotations[j].Y, bone.Rotations[j].Z, bone.Rotations[j].W);
        //            Maxscript.AnimateAtTime(bone.RotationKeys[j], "rotate {0}[{1}] {0}[{1}].transform.rotation", boneArray, boneMap[i] + 1);
        //            Maxscript.AnimateAtTime(bone.RotationKeys[j], "rotate {0}[{1}] {2}", boneArray, boneMap[i] + 1, "bRot");
        //            //Maxscript.AnimateAtTime(bone.RotationKeys[j], "{0}[{1}].rotation = {2}", boneArray, boneMap[i] + 1, "bRot");
        //            if (AnimFile.Bones[i].ParentIndex != i)
        //            {
        //                Maxscript.AnimateAtTime(bone.RotationKeys[j], "rotate {0}[{1}] {0}[{1}].parent.rotation", boneArray, boneMap[i] + 1);
        //                //Maxscript.AnimateAtTime(bone.RotationKeys[j], "rotate {0}[{1}] {0}[{1}].parent.rotation", boneArray, boneMap[i] + 1);
        //                //Maxscript.AtTime(bone.RotationKeys[j],
        //                //    "bRot = -({0}[{1}].parent.rotation * {0}[{1}].rotation)", boneArray, boneMap[i] + 1);
        //                //Maxscript.AnimateAtTime(bone.RotationKeys[j], "{0}[{1}].rotation = {2}", boneArray, boneMap[i] + 1, "bRot");
        //                //Maxscript.AtTime(bone.RotationKeys[j],
        //                //    "bRot = ({0}[{1}].transform * {0}[{1}].parent.transform).rotation", boneArray, boneMap[i] + 1);
        //                //Maxscript.AnimateAtTime(bone.RotationKeys[j], "{0}[{1}].rotation = {2}", boneArray, boneMap[i] + 1, "bRot");
        //                //Maxscript.AnimateAtTime(bone.RotationKeys[j], "{0}[{1}].transform *= {0}[{1}].parent.transform", boneArray, boneMap[i] + 1);
        //            }
        //        }
        //        for (int j = 0; j < bone.PositionKeys.Count; ++j)
        //        {
        //            string bPos = Maxscript.NewPoint3<float>("bPos", bone.Positions[j].X, bone.Positions[j].Y, bone.Positions[j].Z);
        //            Maxscript.AnimateAtTime(bone.PositionKeys[j], "{0}[{1}].position = {2}", boneArray, boneMap[i] + 1, bPos);
        //            if (AnimFile.Bones[i].ParentIndex != i)
        //            {
        //                Maxscript.AtTime(bone.PositionKeys[j],
        //                    "bPos = ({0}[{1}].transform * {0}[{1}].parent.transform).translation", boneArray, boneMap[i] + 1);
        //                Maxscript.AnimateAtTime(bone.PositionKeys[j], "{0}[{1}].position = {2}", boneArray, boneMap[i] + 1, "bPos");
        //                //Maxscript.AnimateAtTime(bone.PositionKeys[j], "{0}[{1}].transform *= {0}[{1}].parent.transform", boneArray, boneMap[i] + 1);
        //            }
        //        }
        //    }
        //}

        public void Export()
        {
            Maxscript.Command("exportStartTime = timeStamp()");
            this.Clear();

            Maxscript.Command("ExportGrnData()");

            this.ExportSkeleton();

            if (this.ExportSetting.HasFlag(GrnExportSetting.Model))
            {
                int meshCount = Maxscript.QueryInteger("grnMeshes.count");
                for (int i = 0; i < meshCount; ++i)
                {
                    this.File.Meshes.Add(new GrnMesh(this.File));
                    this.ExportMesh(i);
                }
            }

            if (this.ExportSetting.HasFlag(GrnExportSetting.Animation))
            {
                this.ExportAnimation();
                if (this.File.Animation.Duration == 0f)
                {
                    this.File.Animation.Duration = 1f;
                }
                this.File.Animation.TimeStep = 1f / 60f;
            }
            else
            {
                this.File.Animation.Duration = 0f;
                this.File.Animation.TimeStep = 1f;
            }

            if (this.ExportSetting.HasFlag(GrnExportSetting.Model))
            {
                int numMaterials = Maxscript.QueryInteger("{0}.material.materialList.count", "mainObject");
                for (int i = 0; i < numMaterials; i++)
                {
                    this.File.Materials.Add(new GrnMaterial(this.File));
                    this.ExportMaterial(i, "mainObject");
                }
            }

            Maxscript.Command("exportEndTime = timeStamp()");
            Maxscript.Format("Export took % seconds\n", "((exportEndTime - exportStartTime) / 1000.0)");
        }
        private void ExportSkeleton()
        {
            int numBones = Maxscript.QueryInteger("grnBones.count");
            this.File.Bones.Add(new GrnBone(this.File));
            this.File.Bones[0].DataExtensionIndex = this.File.AddDataExtension("__Root");
            this.File.Bones[0].Rotation = new Quaternion(1, 0, 0, 0);
            this.File.Bones[0].ParentIndex = 0;

            for (int i = 1; i <= numBones; ++i)
            {
                try
                {
                    GrnBone bone = new GrnBone(this.File);
                    bone.DataExtensionIndex = this.File.AddDataExtension(Maxscript.QueryString("grnBones[{0}].name", i));
                    bone.ParentIndex = Maxscript.QueryInteger("grnBoneParents[{0}]", i);

                    Maxscript.Command("boneTransMat = grnBones[{0}].transform", i);
                    if (bone.ParentIndex > 0)
                    {
                        Maxscript.Command("boneTransMat = boneTransMat * inverse(grnBones[{0}].parent.transform)", i);
                    }

                    Vector3D pos;
                    Quaternion rot;
                    Matrix3x3 scale;
                    this.GetTransformPRS("boneTransMat", out pos, out rot, out scale);
                    bone.Position = pos;
                    bone.Rotation = rot;
                    bone.Scale = scale;

                    this.File.Bones.Add(bone);
                }
                catch (Exception ex)
                {
                    throw new Exception("Bone Index = " + i, ex);
                }
            }
        }
        private void ExportMesh(int meshIndex)
        {
            GrnMesh mesh = this.File.Meshes[meshIndex];
            bool hadEditNormMod = false;
            string mainObject = "mainObject";
            Maxscript.Command("{0} = grnMeshes[{1}]", mainObject, meshIndex + 1);
            string mainMesh = Maxscript.SnapshotAsMesh("mainMesh", mainObject);
            mesh.DataExtensionIndex = this.File.AddDataExtension(Maxscript.QueryString("{0}.name", mainObject));

            // Setup Normals
            Maxscript.Command("max modify mode");
            if (Maxscript.QueryBoolean("{0}.modifiers[#edit_normals] == undefined", mainObject))
            {
                //Maxscript.Command("addModifier {0} (Edit_Normals())", mainObject);
            }
            else { hadEditNormMod = true; }
            //Maxscript.Command("modPanel.setCurrentObject {0}.modifiers[#edit_normals] ui:true", mainObject);
            //Maxscript.Command("CalculateAveragedNormals {0}", mainObject);

            IGlobal global = GlobalInterface.Instance;
            IInterface13 intfc = global.COREInterface13;
            IIGameScene igc = global.IGameInterface;
            igc.InitialiseIGame(false);
            IINode node = global.MAXScriptInterface.GetINodeByHandle((uint)Maxscript.QueryInteger("{0}.handle", mainObject));
            IIGameNode ign = igc.GetIGameNode(node);
            IIGameObject igo = ign.IGameObject;
            IIGameMesh igm = global.IGameMesh.Marshal(igo.NativePointer);
            IMesh im = igm.MaxMesh;

            int numVertices = Maxscript.QueryInteger("meshop.getnumverts {0}", mainMesh);
            int numFaces = Maxscript.QueryInteger("meshop.getnumfaces {0}", mainMesh);

            //if (igm.InitializeData)
            //{
                for (int i = 0; i < numVertices; i++)
                {
                    try
                    {
                        IPoint3 v = igm.GetVertex(i, false);
                        mesh.Vertices.Add(new Vector3D(v.X,v.Y,v.Z));
                        //Maxscript.Command("vertex = meshGetVertFunc {0} {1}", mainMesh, i + 1);
                        //mesh.Vertices.Add(new Vector3D(
                        //    Maxscript.QueryFloat("vertex.x"),
                        //    Maxscript.QueryFloat("vertex.y"),
                        //    Maxscript.QueryFloat("vertex.z")));
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Import Verts/Normals " + i.ToString(), ex);
                    }
                }
            //}

            Maxscript.Command("getVertNormalFunc = {0}.modifiers[#edit_normals].GetNormal", mainObject);
            int numNorms = Maxscript.QueryInteger("{0}.modifiers[#edit_normals].GetNumNormals()", mainObject);
            //igm.SetCreateOptimizedNormalList();
            //MessageBox.Show(igm.NumberOfNormals.ToString());
            IMeshNormalSpec normalSpec = im.SpecifiedNormals;
            if (normalSpec.NumNormals == 0)
            {
                normalSpec.SetParent(im);
                normalSpec.CheckNormals();
            }
            //MessageBox.Show(normalSpec.NumNormals.ToString());
            for (int i = 0; i < normalSpec.NumNormals; ++i)
            {
                IPoint3 n = normalSpec.Normal(i);// igm.GetNormal(i, false);
                mesh.Normals.Add(new Vector3D(n.X,n.Y,n.Z));
                //Maxscript.Command("currentNormal = getVertNormalFunc {0}", i + 1);
                //mesh.Normals.Add(new Vector3D(
                //    Maxscript.QueryFloat("currentNormal.x"),
                //    Maxscript.QueryFloat("currentNormal.y"),
                //    Maxscript.QueryFloat("currentNormal.z")));
            }

            int numTexVertices = Maxscript.QueryInteger("meshop.getnumtverts {0}", mainMesh);
            numTexVertices = im.NumTVerts;
            for (int i = 0; i < numTexVertices; i++)
            {
                IPoint3 tv = im.TVerts[i];
                mesh.TextureCoordinates.Add(new Vector3D(tv.X,tv.Y,tv.Z));
                //Maxscript.Command("tVert = getTVert {0} {1}", mainMesh, i + 1);
                //mesh.TextureCoordinates.Add(new Vector3D(
                //    Maxscript.QueryFloat("tVert.x"),
                //    Maxscript.QueryFloat("tVert.y"),
                //    Maxscript.QueryFloat("tVert.z")));
            }

            Maxscript.Command("meshGetNormalIdFunc = {0}.modifiers[#edit_normals].GetNormalID", mainObject);
            //MessageBox.Show(normalSpec.NumFaces.ToString());
            for (int i = 0; i < numFaces; ++i)
            {
                Face f = new Face();
                f.MaterialIndex = (Int16)im.GetFaceMtlIndex(i);
                //f.MaterialIndex = (Int16)(Maxscript.QueryInteger("getFaceMatID {0} {1}", mainMesh, i + 1) - 1);

                IFace ff = im.Faces[i];
                f.Indices.Add((Int16)ff.GetVert(0));
                f.Indices.Add((Int16)ff.GetVert(1));
                f.Indices.Add((Int16)ff.GetVert(2));
                //Maxscript.Command("face = getFace {0} {1}", mainMesh, i + 1);
                //f.Indices.Add((Int16)(Maxscript.QueryInteger("face.x") - 1));
                //f.Indices.Add((Int16)(Maxscript.QueryInteger("face.y") - 1));
                //f.Indices.Add((Int16)(Maxscript.QueryInteger("face.z") - 1));

                IMeshNormalFace nf = normalSpec.Face(i);
                f.NormalIndices.Add(nf.GetNormalID(0));
                f.NormalIndices.Add(nf.GetNormalID(1));
                f.NormalIndices.Add(nf.GetNormalID(2));
                //f.NormalIndices.Add(Maxscript.QueryInteger("meshGetNormalIdFunc {0} {1}", i + 1, 1) - 1);
                //f.NormalIndices.Add(Maxscript.QueryInteger("meshGetNormalIdFunc {0} {1}", i + 1, 2) - 1);
                //f.NormalIndices.Add(Maxscript.QueryInteger("meshGetNormalIdFunc {0} {1}", i + 1, 3) - 1);

                ITVFace tf = im.TvFace[i];
                f.TextureIndices.Add((int)tf.GetTVert(0));
                f.TextureIndices.Add((int)tf.GetTVert(1));
                f.TextureIndices.Add((int)tf.GetTVert(2));
                //Maxscript.Command("tFace = getTVFace {0} {1}", mainMesh, i + 1);
                //f.TextureIndices.Add(Maxscript.QueryInteger("tFace.x") - 1);
                //f.TextureIndices.Add(Maxscript.QueryInteger("tFace.y") - 1);
                //f.TextureIndices.Add(Maxscript.QueryInteger("tFace.z") - 1);
                mesh.Faces.Add(f);
            }
            // Delete normals mod if it wasn't there in the first place
            if (!hadEditNormMod)
            {
                Maxscript.Command("deleteModifier {0} {0}.modifiers[#edit_normals]", mainObject);
            }

            IIGameSkin igs = igm.IGameSkin;
            if (Maxscript.QueryBoolean("{0}.modifiers[#skin] != undefined", mainObject))
            {
                Maxscript.Command("skinMod = {0}.modifiers[#skin]", mainObject);
                Maxscript.Command("modPanel.setCurrentObject skinMod ui:true");
                Maxscript.Command("ExportSkinData()");
                int numBVerts = Maxscript.QueryInteger("grnSkinWeights.count");
                numBVerts = igs.NumOfSkinnedVerts;
                for (int i = 0; i < numBVerts; ++i)
                {
                    mesh.VertexWeights.Add(new VertexWeight());
                    Maxscript.Command("skinWeightArray = grnSkinWeights[{0}]", i + 1);
                    int numVWs = Maxscript.QueryInteger("skinWeightArray.count");
                    numVWs = igs.GetNumberOfBones(i);
                    for (int j = 0; j < numVWs; ++j)
                    {
                        if (i == 0)
                        {
                            MessageBox.Show(numVWs.ToString());
                            MessageBox.Show(igs.GetBoneID(0, 0).ToString());
                            MessageBox.Show(igs.GetBoneIndex(igs.GetBone(igs.GetBoneID(0, 0), true), true).ToString());
                        }
                        mesh.VertexWeights[i].BoneIndices.Add(igs.GetBoneID(i, j) - 1);
                        mesh.VertexWeights[i].Weights.Add(igs.GetWeight(i, j));
                        //mesh.VertexWeights[i].BoneIndices.Add(Maxscript.QueryInteger("skinWeightArray[{0}][1]", j + 1));
                        //mesh.VertexWeights[i].Weights.Add(Maxscript.QueryFloat("skinWeightArray[{0}][2]", j + 1));
                    }
                }

                int numSkinBBs = Maxscript.QueryInteger("grnSkinBBMaxs.count");
                for (int i = 0; i < numSkinBBs; ++i)
                {
                    Maxscript.Command("bbMax = grnSkinBBMaxs[{0}]", i + 1);
                    Maxscript.Command("bbMin = grnSkinBBMins[{0}]", i + 1);
                    mesh.BoneBindings.Add(new GrnBoneBinding());
                    mesh.BoneBindings[i].BoneIndex = Maxscript.QueryInteger("grnSkinBBIndices[{0}]", i + 1);
                    mesh.BoneBindings[i].OBBMax = new Vector3D(
                        Maxscript.QueryFloat("bbMax.x"),
                        Maxscript.QueryFloat("bbMax.y"),
                        Maxscript.QueryFloat("bbMax.z"));
                    mesh.BoneBindings[i].OBBMin = new Vector3D(
                        Maxscript.QueryFloat("bbMin.x"),
                        Maxscript.QueryFloat("bbMin.y"),
                        Maxscript.QueryFloat("bbMin.z"));
                }
            }

            //normalSpec.ReleaseInterface();
            ign.ReleaseIGameObject();
            node.ReleaseInterface();
            //gnode.ReleaseIGameObject();
            igc.ReleaseIGame();
            intfc.ReleaseInterface();
        }
        private void ExportAnimation()
        {
            int numBones = Maxscript.QueryInteger("grnBones.count");
            this.File.Animation.Duration = 0f;
            GrnBoneTrack rootBoneTrack = new GrnBoneTrack();
            HashSet<float> rootTrackKeys = new HashSet<float>();
            rootBoneTrack.DataExtensionIndex = this.File.Bones[0].DataExtensionIndex;
            this.File.Animation.BoneTracks.Add(rootBoneTrack);
            Maxscript.Command("grnBoneAnimKeys = #()");

            for (int i = 1; i <= numBones; ++i)
            {
                //if (Maxscript.QueryBoolean("grnBones[{0}].isanimated == false", i))
                //{
                //    continue;
                //}

                GrnBoneTrack bone = new GrnBoneTrack();
                bone.DataExtensionIndex = this.File.Bones[i].DataExtensionIndex;

                Maxscript.Command("GetBoneAnimKeys grnBones[{0}]", i);
                Maxscript.Command("append grnBoneAnimKeys keys");
                if (this.File.Bones[i].ParentIndex > 0 &&
                    (Maxscript.QueryBoolean("classof grnBones[{0}] == Biped_Object", i) ||
                    Maxscript.QueryBoolean("classof grnBones[{0}][3].controller == BipSlave_Control", i) ||
                    Maxscript.QueryBoolean("classof grnBones[{0}][3].controller == Vertical_Horizontal_Turn", i)))
                {
                    Maxscript.Command("join keys grnBoneAnimKeys[grnBoneParents[{0}]]", i);
                    Maxscript.Command("keys = makeUniqueArray keys");
                    Maxscript.Command("sort keys");
                    Maxscript.Command("grnBoneAnimKeys[{0}] = keys", i);

                    //Maxscript.Command("posParentNode = biped.getPosParentNode grnBones[{0}]", i);
                    //Maxscript.Command("rotParentNode = biped.getRotParentNode grnBones[{0}]", i);
                    //if (Maxscript.QueryBoolean("grnBones[{0}].parent != posParentNode", i) ||
                    //    Maxscript.QueryBoolean("grnBones[{0}].parent != rotParentNode", i))
                    //{
                    //    //Maxscript.Command("origKeys = deepcopy keys");
                    //    //Maxscript.Command("GetBoneAnimKeys grnBones[grnBoneParents[{0}]]", i);
                    //    //Maxscript.Command("join keys origKeys");
                    //    //Maxscript.Command("keys = makeUniqueArray keys");
                    //    //Maxscript.Command("sort keys");
                    //}
                }

                int numKeys = Maxscript.QueryInteger("keys.count");
                float startTime = Maxscript.QueryFloat("animationRange.start.ticks / 4800.0");
                float time = Maxscript.QueryFloat("keys[1]");
                Vector3D pos = new Vector3D();
                Quaternion rot = new Quaternion();
                Matrix3x3 scale = Matrix3x3.Identity;

                if (numKeys > 0)
                {
                    if (Maxscript.QueryBoolean("classof grnBones[{0}] == Dummy", i))
                    {
                        Maxscript.SetVarAtTime(time + startTime, "boneTransMat", "grnBones[{0}][3].controller.value", i);
                    }
                    else if (this.File.Bones[i].ParentIndex > 0)
                    {
                        Maxscript.SetVarAtTime(time + startTime, "boneTransMat", "grnBones[{0}].transform", i);
                        Maxscript.SetVarAtTime(time + startTime, "bonePTransMat", "grnBones[{0}].parent.transform", i);
                        Maxscript.Command("boneTransMat = boneTransMat * inverse(bonePTransMat)");
                    }
                    else
                    {
                        Maxscript.SetVarAtTime(time + startTime, "boneTransMat", "grnBones[{0}].transform", i);
                    }
                    this.GetTransformPRS("boneTransMat", out pos, out rot, out scale);
                    //Maxscript.SetVarAtTime(time + startTime, "boneTransMat", "grnBones[{0}][3].controller.value", i);
                    //if (this.File.Bones[i].ParentIndex > 0 && 
                    //    (Maxscript.QueryBoolean("classof grnBones[{0}] == Biped_Object", i) ||
                    //    Maxscript.QueryBoolean("classof grnBones[{0}][3].controller == BipSlave_Control", i) ||
                    //    Maxscript.QueryBoolean("classof grnBones[{0}][3].controller == Vertical_Horizontal_Turn", i)))
                    //{
                    //    //Vector3D posPNode = new Vector3D();
                    //    //Maxscript.SetVarAtTime(time + startTime, "boneTransMatRot", "boneTransMat * inverse(grnBones[{0}].parent[3].controller.value)", i);
                    //    //Maxscript.SetVarAtTime(time + startTime, "boneTransMatPos", "boneTransMat * inverse(posParentNode[3].controller.value)");
                    //    //Maxscript.SetVarAtTime(time + startTime, "boneTransMatRot", "boneTransMat * inverse(grnBones[{0}].parent[3].controller.value)", i);
                    //    //Maxscript.SetVarAtTime(time + startTime, "boneTransMatRot", "boneTransMat * inverse(rotParentNode[3].controller.value) * inverse(posParentNode[3].controller.value)");
                    //    //if (Maxscript.QueryBoolean("grnBones[{0}].parent != posParentNode", i))
                    //    //{
                    //    //    Maxscript.SetVarAtTime(time + startTime, "boneTransMatRot", "boneTransMatRot * inverse(transmatrix grnBones[{0}].parent[3].controller.value.translation)", i);
                    //    //}
                    //    //if (Maxscript.QueryBoolean("grnBones[{0}].parent != rotParentNode", i))
                    //    //{
                    //    //    Maxscript.SetVarAtTime(time + startTime, "boneTransMatRot", "boneTransMatRot * inverse(grnBones[{0}].parent[3].controller.value.rotation as matrix3)", i);
                    //    //}
                    //    //this.GetTransformPRS("boneTransMatPos", out posPNode, out rot, out scale);
                    //    Maxscript.SetVarAtTime(time + startTime, "bonePTransMat", "grnBones[{0}].parent[3].controller.value", i);
                    //    Maxscript.Command("boneTransMat = boneTransMat * inverse(bonePTransMat)");
                    //    this.GetTransformPRS("boneTransMat", out pos, out rot, out scale);
                    //    //pos = posPNode;
                    //    //Maxscript.SetVarAtTime(time + startTime, "boneTransMat", "boneTransMat * inverse(grnBones[{0}].parent.transform)", i);
                    //    ////Maxscript.SetVarAtTime(time + startTime, "boneTransMat", "grnBones[{0}][3].controller.value * inverse(grnBones[{0}].parent[3].controller.value)", i);
                    //    //this.GetTransformPRS("boneTransMat", out pos, out rot, out scale);
                    //}
                    //else
                    //{
                    //    this.GetTransformPRS("boneTransMat", out pos, out rot, out scale);
                    //}

                    bone.PositionKeys.Add(time);
                    bone.Positions.Add(pos);
                    bone.RotationKeys.Add(time);
                    bone.Rotations.Add(rot);
                    bone.ScaleKeys.Add(time);
                    bone.Scales.Add(scale);
                }

                Vector3D posCurrent = new Vector3D();
                Quaternion rotCurrent = new Quaternion();
                Matrix3x3 scaleCurrent = Matrix3x3.Identity;
                for (int j = 1; j < numKeys; ++j)
                {
                    time = Maxscript.QueryFloat("keys[{0}]", j + 1);
                    if (Maxscript.QueryBoolean("classof grnBones[{0}] == Dummy", i))
                    {
                        Maxscript.SetVarAtTime(time + startTime, "boneTransMat", "grnBones[{0}][3].controller.value", i);
                    }
                    else if (this.File.Bones[i].ParentIndex > 0)
                    {
                        Maxscript.SetVarAtTime(time + startTime, "boneTransMat", "grnBones[{0}].transform", i);
                        Maxscript.SetVarAtTime(time + startTime, "bonePTransMat", "grnBones[{0}].parent.transform", i);
                        Maxscript.Command("boneTransMat = boneTransMat * inverse(bonePTransMat)");
                    }
                    else
                    {
                        Maxscript.SetVarAtTime(time + startTime, "boneTransMat", "grnBones[{0}].transform", i);
                    }
                    this.GetTransformPRS("boneTransMat", out posCurrent, out rotCurrent, out scaleCurrent);
                    //Maxscript.SetVarAtTime(time + startTime, "boneTransMat", "grnBones[{0}][3].controller.value", i);
                    //if (this.File.Bones[i].ParentIndex > 0 &&
                    //    (Maxscript.QueryBoolean("classof grnBones[{0}] == Biped_Object", i) ||
                    //    Maxscript.QueryBoolean("classof grnBones[{0}][3].controller == BipSlave_Control", i) ||
                    //    Maxscript.QueryBoolean("classof grnBones[{0}][3].controller == Vertical_Horizontal_Turn", i)))
                    //{
                    //    //Vector3D posPNode = new Vector3D();
                    //    //Maxscript.SetVarAtTime(time + startTime, "boneTransMatPos", "boneTransMat * inverse(posParentNode[3].controller.value)");
                    //    //this.GetTransformPRS("boneTransMatPos", out posPNode, out rotCurrent, out scaleCurrent);
                    //    //Maxscript.SetVarAtTime(time + startTime, "boneTransMatRot", "boneTransMat * inverse(rotParentNode[3].controller.value)");
                    //    //this.GetTransformPRS("boneTransMatRot", out posCurrent, out rotCurrent, out scaleCurrent);
                    //    //posCurrent = posPNode;
                    //    //sliderTime = frameNum
                    //    //forceCompleteRedraw()
                    //    //Maxscript.Command("sliderTime = {0}s", time + startTime);
                    //    //Maxscript.Command("forceCompleteRedraw()");
                    //    Maxscript.SetVarAtTime(time + startTime, "boneTransMat", "grnBones[{0}].transform", i);
                    //    Maxscript.SetVarAtTime(time + startTime, "bonePTransMat", "grnBones[{0}].parent.transform", i);
                    //    Maxscript.Command("boneTransMat = boneTransMat * inverse(bonePTransMat)", i);
                    //    //Maxscript.SetVarAtTime(time + startTime, "boneTransMat", "grnBones[{0}][3].controller.value * inverse(grnBones[{0}].parent[3].controller.value)", i);
                    //    this.GetTransformPRS("boneTransMat", out posCurrent, out rotCurrent, out scaleCurrent);
                    //}
                    //else
                    //{
                    //    this.GetTransformPRS("boneTransMat", out posCurrent, out rotCurrent, out scaleCurrent);
                    //}

                    if (pos != posCurrent || j + 1 == numKeys)
                    {
                        bone.PositionKeys.Add(time);
                        bone.Positions.Add(posCurrent);
                        pos = posCurrent;
                    }

                    if (rot != rotCurrent || j + 1 == numKeys)
                    {
                        bone.RotationKeys.Add(time);
                        bone.Rotations.Add(rotCurrent);
                        rot = rotCurrent;
                    }

                    if (!this.AreEqual(scale, scaleCurrent) || j + 1 == numKeys) //if (scale != scaleCurrent)
                    {
                        bone.ScaleKeys.Add(time);
                        bone.Scales.Add(scaleCurrent);
                        scale = scaleCurrent;
                    }
                }

                //int numPosKeys = Maxscript.QueryInteger("grnBones[{0}][3][1].controller.keys.count", i);
                //Maxscript.Command("sort grnBones[{0}][3][1].controller.keys", i);
                //for (int j = 0; j < numPosKeys; ++j)
                //{
                //    try
                //    {
                //        bone.PositionKeys.Add(Maxscript.QueryFloat("grnBones[{0}][3][1].controller.keys[{1}].time as float / 4800", i, j + 1));
                //        bone.Positions.Add(new Vector3D(
                //            Maxscript.QueryFloat("grnBones[{0}][3][1][1].controller.keys[{1}].value", i, j + 1),
                //            Maxscript.QueryFloat("grnBones[{0}][3][1][2].controller.keys[{1}].value", i, j + 1),
                //            Maxscript.QueryFloat("grnBones[{0}][3][1][3].controller.keys[{1}].value", i, j + 1)));
                //    }
                //    catch (Exception ex) { throw new Exception("Pos Controller Bone " + i + " key " + (j + 1), ex); }
                //}

                //int numRotKeys = Maxscript.QueryInteger("grnBones[{0}][3][2].controller.keys.count", i);
                //for (int j = 0; j < numRotKeys; ++j)
                //{
                //    bone.RotationKeys.Add(Maxscript.QueryFloat("grnBones[{0}][3][2].controller.keys[{1}].time as float / 4800", i, j + 1));
                //    Maxscript.Command("rotQuat = grnBones[{0}][3][2].controller.keys[{1}].value as quat", i, j + 1);
                //    bone.Rotations.Add(new Quaternion(
                //        Maxscript.QueryFloat("rotQuat.w"),
                //        Maxscript.QueryFloat("rotQuat.x"),
                //        Maxscript.QueryFloat("rotQuat.y"),
                //        Maxscript.QueryFloat("rotQuat.z")));
                //}

                //int numScaleKeys = Maxscript.QueryInteger("grnBones[{0}][3][3].controller.keys.count", i);
                //for (int j = 0; j < numScaleKeys; ++j)
                //{
                //    bone.ScaleKeys.Add(Maxscript.QueryFloat("grnBones[{0}][3][3].controller.keys[{1}].time as float / 4800", i, j + 1));
                //    Maxscript.Command("bTrackScale = grnBones[{0}][3][3].controller.keys[{1}].value", i, j + 1);
                //    Matrix3x3 scaleMatrix = new Matrix3x3();
                //    scaleMatrix.A1 = Maxscript.QueryFloat("bTrackScale.x");
                //    scaleMatrix.B2 = Maxscript.QueryFloat("bTrackScale.y");
                //    scaleMatrix.C3 = Maxscript.QueryFloat("bTrackScale.z");
                //    bone.Scales.Add(scaleMatrix);
                //}

                //this.Plugin.richTextBox1.AppendText(i + " " + Maxscript.QueryString("grnBones[{0}].name", i) + Environment.NewLine);
                //this.Plugin.richTextBox1.AppendText(bone.Rotations[0].ToString() + Environment.NewLine);
                this.File.Animation.BoneTracks.Add(bone);
                this.File.Animation.Duration = Math.Max(this.File.Animation.Duration, bone.PositionKeys.Last());
                this.File.Animation.Duration = Math.Max(this.File.Animation.Duration, bone.RotationKeys.Last());
                this.File.Animation.Duration = Math.Max(this.File.Animation.Duration, bone.ScaleKeys.Last());
            }

            // Add keys for the root node at 0, middle, end
            rootTrackKeys.Add(0);
            rootTrackKeys.Add(this.File.Animation.Duration / 2f);
            rootTrackKeys.Add(this.File.Animation.Duration);
            rootBoneTrack.PositionKeys.AddRange(rootTrackKeys);
            rootBoneTrack.RotationKeys.AddRange(rootTrackKeys);
            rootBoneTrack.ScaleKeys.AddRange(rootTrackKeys);
            for (int i = 0; i < rootTrackKeys.Count; ++i)
            {
                rootBoneTrack.Positions.Add(new Vector3D(0, 0, 0));
                rootBoneTrack.Rotations.Add(new Quaternion(1, 0, 0, 0));
                rootBoneTrack.Scales.Add(Matrix3x3.Identity);
            }
        }
        private void ExportMaterial(int matIndex, string mainObject)
        {
            GrnMaterial mat = this.File.Materials[matIndex];
            Maxscript.Command("mat = {0}.material[{1}]", mainObject, matIndex + 1);
            mat.DataExtensionIndex = this.File.AddDataExtension(Maxscript.QueryString("mat.name"));

            mat.DiffuseColor = new Color3D(Maxscript.QueryFloat("mat.diffuse.r") / 255f,
                Maxscript.QueryFloat("mat.diffuse.g") / 255f,
                Maxscript.QueryFloat("mat.diffuse.b") / 255f);
            mat.AmbientColor = new Color3D(Maxscript.QueryFloat("mat.ambient.r") / 255f,
                Maxscript.QueryFloat("mat.ambient.g") / 255f,
                Maxscript.QueryFloat("mat.ambient.b") / 255f);
            mat.SpecularColor = new Color3D(Maxscript.QueryFloat("mat.specular.r") / 255f,
                Maxscript.QueryFloat("mat.specular.g") / 255f,
                Maxscript.QueryFloat("mat.specular.b") / 255f);
            mat.EmissiveColor = new Color3D(Maxscript.QueryFloat("mat.selfIllumColor.r") / 255f,
                Maxscript.QueryFloat("mat.selfIllumColor.g") / 255f,
                Maxscript.QueryFloat("mat.selfIllumColor.b") / 255f);
            mat.Opacity = Maxscript.QueryFloat("mat.opacity") / 100f;
            mat.SpecularExponent = Maxscript.QueryFloat("mat.specularLevel");
            int opacityType = Maxscript.QueryInteger("mat.opacityType");

            if (Maxscript.QueryBoolean("(classof mat.diffusemap) == BitmapTexture"))
            {
                GrnTexture tex = new GrnTexture(this.File);
                tex.DataExtensionIndex = this.File.AddDataExtension(Maxscript.QueryString("mat.diffusemap.name"));
                this.File.SetDataExtensionFileName(tex.DataExtensionIndex, Path.GetFileName(Maxscript.QueryString("mat.diffusemap.filename")));

                int texIndex = this.File.Textures.IndexOf(tex);
                if (texIndex >= 0)
                {
                    mat.DiffuseTextureIndex = texIndex;
                }
                else
                {
                    mat.DiffuseTextureIndex = this.File.Textures.Count;
                    this.File.Textures.Add(tex);
                }
            }
            else if (Maxscript.QueryBoolean("(classof mat.diffusemap) == CompositeTextureMap") && Maxscript.QueryBoolean("(classof mat.diffusemap.mapList[1]) == BitmapTexture"))
            {
                GrnTexture tex = new GrnTexture(this.File);
                tex.DataExtensionIndex = this.File.AddDataExtension(Maxscript.QueryString("mat.diffusemap.mapList[1].name"));
                this.File.SetDataExtensionFileName(tex.DataExtensionIndex, Path.GetFileName(Maxscript.QueryString("mat.diffusemap.mapList[1].filename")));

                int texIndex = this.File.Textures.IndexOf(tex);
                if (texIndex >= 0)
                {
                    mat.DiffuseTextureIndex = texIndex;
                    this.File.DataExtensions.RemoveAt(tex.DataExtensionIndex);
                }
                else
                {
                    mat.DiffuseTextureIndex = this.File.Textures.Count;
                    this.File.Textures.Add(tex);
                }
            }
        }

        private string CreateBone(GrnBone bone)
        {
            string boneNode = "boneNode";

            Maxscript.Command("boneNode = dummy name:\"{0}\" boxsize:[0.25,0.25,0.25]", bone.Name);
            Maxscript.Command("boneNode.transform = {0}", this.GetBoneLocalTransform(bone, "boneTransMat"));

            //this.GetBoneLocalTransform(bone, "tfm");
            //Maxscript.Command("boneNode = bonesys.createbone tfm.row4 (tfm.row4 + 0.01 * (normalize tfm.row1)) (normalize tfm.row3)");
            //Maxscript.Command("boneNode.name = \"{0}\"", bone.Name);
            //Maxscript.Command("boneNode.width = 0.1");
            //Maxscript.Command("boneNode.height = 0.1");
            //Maxscript.Command("boneNode.wirecolor = yellow");
            //Maxscript.Command("boneNode.showlinks = true");
            //Maxscript.Command("boneNode.setBoneEnable false 0");
            //Maxscript.Command("boneNode.pos.controller = TCB_position ()");
            //Maxscript.Command("boneNode.rotation.controller = TCB_rotation ()");

            return boneNode;
        }
        private string GetBoneLocalTransform(GrnBone bone, string nameM3)
        {
            Maxscript.Command("{0} = matrix3 1", nameM3);
            Maxscript.Command("{0} = transmatrix {1}", nameM3, Maxscript.Point3Literal(bone.Position));
            Maxscript.Command("{0} = (inverse(quat {1} {2} {3} {4}) as matrix3) * {0}", nameM3, bone.Rotation.X, bone.Rotation.Y, bone.Rotation.Z, bone.Rotation.W);
            Maxscript.Command("{0} = (matrix3 [{1}, {2}, {3}] [{4}, {5}, {6}] [{7}, {8}, {9}] [0,0,0]) * {0}", nameM3,
                bone.Scale.A1, bone.Scale.A2, bone.Scale.A3,
                bone.Scale.B1, bone.Scale.B2, bone.Scale.B3,
                bone.Scale.C1, bone.Scale.C2, bone.Scale.C3);

            return nameM3;
        }
        private string GetBoneLocalTransform(string nameM3, Vector3D pos, Quaternion rot, Matrix3x3 scale)
        {
            Maxscript.Command("{0} = matrix3 1", nameM3);
            Maxscript.Command("{0} = transmatrix {1}", nameM3, Maxscript.Point3Literal(pos));
            Maxscript.Command("{0} = (inverse(quat {1} {2} {3} {4}) as matrix3) * {0}", nameM3, rot.X, rot.Y, rot.Z, rot.W);
            Maxscript.Command("{0} = (matrix3 [{1}, {2}, {3}] [{4}, {5}, {6}] [{7}, {8}, {9}] [0,0,0]) * {0}", nameM3,
                scale.A1, scale.A2, scale.A3,
                scale.B1, scale.B2, scale.B3,
                scale.C1, scale.C2, scale.C3);

            return nameM3;
        }
        private void GetTransformPRS(string nameM3, out Vector3D pos, out Quaternion rot, out Matrix3x3 scale)
        {
            Maxscript.Command("bRot = inverse({0}.rotation)", nameM3);
            Maxscript.Command("boneScTransMat = {0} * inverse({0}.rotation as matrix3)", nameM3);
            Maxscript.Command("{0} = inverse({0}.rotation as matrix3) * {0}", nameM3);
            Maxscript.Command("bPos = {0}.position", nameM3);

            pos = new Vector3D(
                    Maxscript.QueryFloat("bPos.x"),
                    Maxscript.QueryFloat("bPos.y"),
                    Maxscript.QueryFloat("bPos.z"));
            rot = new Quaternion(
                Maxscript.QueryFloat("bRot.w"),
                Maxscript.QueryFloat("bRot.x"),
                Maxscript.QueryFloat("bRot.y"),
                Maxscript.QueryFloat("bRot.z"));
            scale = new Matrix3x3();
            scale.A1 = Maxscript.QueryFloat("boneScTransMat.row1.x");
            scale.A2 = Maxscript.QueryFloat("boneScTransMat.row1.y");
            scale.A3 = Maxscript.QueryFloat("boneScTransMat.row1.z");
            scale.B1 = Maxscript.QueryFloat("boneScTransMat.row2.x");
            scale.B2 = Maxscript.QueryFloat("boneScTransMat.row2.y");
            scale.B3 = Maxscript.QueryFloat("boneScTransMat.row2.z");
            scale.C1 = Maxscript.QueryFloat("boneScTransMat.row3.x");
            scale.C2 = Maxscript.QueryFloat("boneScTransMat.row3.y");
            scale.C3 = Maxscript.QueryFloat("boneScTransMat.row3.z");
        }
        private string GetBoneWorldTransform(GrnFile file, int boneIndex, string nameM3)
        {
            GrnBone bone = file.Bones[boneIndex];
            if (bone.ParentIndex != boneIndex)
            {
                Maxscript.Command("{0} = {1} * {2}",
                    nameM3, this.GetBoneLocalTransform(bone, nameM3 + boneIndex), 
                    GetBoneWorldTransform(file, bone.ParentIndex, nameM3 + bone.ParentIndex));
            }
            else
            {
                Maxscript.Command("{0} = {1}", nameM3, this.GetBoneLocalTransform(bone, nameM3 + boneIndex));
            }

            return nameM3;
        }
        private bool AreEqual(Matrix3x3 m1, Matrix3x3 m2)
        {
            float epsilon = 10e-6f;

            return (Math.Abs(m1.A1 - m2.A1) < epsilon) &&
                (Math.Abs(m1.A2 - m2.A2) < epsilon) &&
                (Math.Abs(m1.A3 - m2.A3) < epsilon) &&
                (Math.Abs(m1.B1 - m2.B1) < epsilon) &&
                (Math.Abs(m1.B2 - m2.B2) < epsilon) &&
                (Math.Abs(m1.B3 - m2.B3) < epsilon) &&
                (Math.Abs(m1.C1 - m2.C1) < epsilon) &&
                (Math.Abs(m1.C2 - m2.C2) < epsilon) &&
                (Math.Abs(m1.C3 - m2.C3) < epsilon);
        }
        #endregion

        #region UI
        public void LoadUI()
        {
            this.Plugin.Text = MaxPluginForm.PluginTitle + " - " + Path.GetFileName(this.FileName);

            this.Plugin.grnObjectsTreeListView.ClearObjects();
            if (this.File.Bones.Count > 0)
            {
                this.Plugin.grnObjectsTreeListView.AddObject(this.File.Bones[0]);
            }
            this.Plugin.grnObjectsTreeListView.AddObjects(this.File.Meshes);
            this.Plugin.grnObjectsTreeListView.AddObjects(this.File.Materials);

            int totalVerts = 0;
            int totalFaces = 0;
            for (int i = 0; i < this.File.Meshes.Count; ++i)
            {
                totalVerts += this.File.Meshes[i].Vertices.Count;
                totalFaces += this.File.Meshes[i].Faces.Count;
            }
            this.Plugin.vertsValueToolStripStatusLabel.Text = totalVerts.ToString();
            this.Plugin.facesValueToolStripStatusLabel.Text = totalFaces.ToString();
            this.Plugin.meshesValueToolStripStatusLabel.Text = this.File.Meshes.Count.ToString();
            this.Plugin.matsValueToolStripStatusLabel.Text = this.File.Materials.Count.ToString();
            this.Plugin.animLengthValueToolStripStatusLabel.Text = this.File.Animation.Duration.ToString();

            this.Plugin.grnExportModelCheckBox.Checked = this.ExportSetting.HasFlag(GrnExportSetting.Model);
            this.Plugin.grnExportAnimCheckBox.Checked = this.ExportSetting.HasFlag(GrnExportSetting.Animation);
        }

        public void SaveUI()
        {
            // Export Settings
            this.ExportSetting = (GrnExportSetting)0;
            if (this.Plugin.grnExportModelCheckBox.Checked)
            {
                this.ExportSetting |= GrnExportSetting.Model;
            }
            if (this.Plugin.grnExportAnimCheckBox.Checked)
            {
                this.ExportSetting |= GrnExportSetting.Animation;
            }
        }
        #endregion

        [Flags]
        private enum GrnExportSetting
        {
            Model = 0x1,
            Animation = 0x2
        }
    }
}
