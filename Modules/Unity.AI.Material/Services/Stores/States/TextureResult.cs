﻿using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Unity.AI.Material.Services.Stores.States
{
    [Serializable]
    record TextureResult
    {
        [SerializeField, JsonProperty]
        string url; // use .uri to get/set this

        [JsonIgnore]
        Uri m_CachedUri;

        internal TextureResult() {}

        protected TextureResult(Uri uri) => this.uri = uri;

        protected TextureResult(string url) => this.url = url;

        [JsonIgnore]
        public Uri uri
        {
            get => m_CachedUri ??= !string.IsNullOrEmpty(url) ? new Uri(url) : null;
            set
            {
                m_CachedUri = value;
                url = m_CachedUri?.ToString();
            }
        }

        public static TextureResult FromPath(string path) => new() { uri = new Uri(Path.GetFullPath(path)) };
        public static TextureResult FromUrl(string url) => new() { uri = new Uri(url) };

        public virtual bool Equals(TextureResult other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return uri?.Equals(other.uri) ?? other.uri is null;
        }

        // ReSharper disable once NonReadonlyMemberInGetHashCode
        public override int GetHashCode() => uri?.GetHashCode() ?? 0;

        public override string ToString() => uri?.ToString() ?? string.Empty;
    }
}
