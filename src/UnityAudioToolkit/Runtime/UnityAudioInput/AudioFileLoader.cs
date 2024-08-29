using System;
using System.IO;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityAudioToolkit
{
    public static class AudioFileLoader
    {
        public static async Task<AudioClip> LoadAsync(string path)
        {
            var request = UnityWebRequestMultimedia.GetAudioClip(path, GetAudioType(path));
            await request.SendWebRequest();
            return  DownloadHandlerAudioClip.GetContent(request);
        }

        public static AudioType GetAudioType(string path)
        {
            var extension = (Path.GetExtension(path).ToLower());
            switch (extension)
            {
                case ".wav":
                    return AudioType.WAV;
                case ".mp3":
                    return AudioType.MPEG;
                default:
                    throw new NotSupportedException($"Unsupported audio file extension: {extension}");
            }
        }
    }
}
