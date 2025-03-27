using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Generators.Asset;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Generators.UI.Utilities
{
    class TemporaryAsset : IDisposable
    {
        public class Scope : IDisposable
        {
            public List<TemporaryAsset> assets { get; }

            public Scope(IEnumerable<TemporaryAsset> assets)
            {
                this.assets = assets.ToList();
            }

            public void Dispose()
            {
                foreach (var asset in assets)
                {
                    asset?.Dispose();
                }
                assets.Clear();
            }
        }

        public AssetReference asset { get; }

        public string tempFolder { get; }

        bool m_Disposed;

        public TemporaryAsset(AssetReference asset, string tempFolder = "")
        {
            this.asset = asset;
            this.tempFolder = tempFolder;
        }

        public void Dispose()
        {
            if (m_Disposed)
                return;

            try
            {
                if (AssetDatabase.IsValidFolder(tempFolder))
                {
                    AssetDatabase.DeleteAsset(tempFolder);
                }
                else if (File.Exists(asset.GetPath()))
                {
                    File.Delete(asset.GetPath());
                }
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error cleaning up temporary asset '{asset}': {ex}");
            }
            finally
            {
                m_Disposed = true;
            }
        }
    }
}
