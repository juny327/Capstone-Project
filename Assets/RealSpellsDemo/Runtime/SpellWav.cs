using System;
using System.IO;
using UnityEngine;

namespace RealSpellsDemo
{
    /// <summary>Reads PCM 16/24/32-bit and float-32 WAV files (mono or stereo, any rate) into an AudioClip, without
    /// the editor or UnityWebRequest, so the run-time loader can play sounds straight from disk.</summary>
    public static class SpellWav
    {
        public static AudioClip Load(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            int channels, rate;
            float[] samples = Decode(b, out channels, out rate, path);
            int frames = samples.Length / Mathf.Max(1, channels);
            var clip = AudioClip.Create(Path.GetFileNameWithoutExtension(path), Mathf.Max(1, frames), channels, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static float[] Decode(byte[] b, out int channels, out int rate, string name = "wav")
        {
            if (b.Length < 44 || b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F' || b[8] != 'W' || b[9] != 'A' || b[10] != 'V' || b[11] != 'E')
                throw new FormatException(name + ": not a RIFF/WAVE file");
            int pos = 12;
            int format = 0, bits = 0;
            channels = 0; rate = 0;
            int dataPos = -1, dataLen = 0;
            while (pos + 8 <= b.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(b, pos, 4);
                int len = BitConverter.ToInt32(b, pos + 4);
                int body = pos + 8;
                if (id == "fmt ")
                {
                    format = BitConverter.ToUInt16(b, body);
                    channels = BitConverter.ToUInt16(b, body + 2);
                    rate = BitConverter.ToInt32(b, body + 4);
                    bits = BitConverter.ToUInt16(b, body + 14);
                    if (format == 0xFFFE && len >= 26) format = BitConverter.ToUInt16(b, body + 24);
                }
                else if (id == "data")
                {
                    dataPos = body;
                    dataLen = Math.Min(len, b.Length - body);
                    break;
                }
                pos = body + len + (len & 1);
            }
            if (dataPos < 0 || channels <= 0 || rate <= 0) throw new FormatException(name + ": missing fmt or data chunk");
            int bytesPer = bits / 8;
            if (bytesPer <= 0) throw new FormatException(name + ": bad bit depth " + bits);
            int count = dataLen / bytesPer;
            var s = new float[count];
            if (format == 3 && bits == 32)
            {
                for (int i = 0; i < count; i++) s[i] = BitConverter.ToSingle(b, dataPos + 4 * i);
            }
            else if (format == 1 && bits == 16)
            {
                for (int i = 0; i < count; i++) s[i] = BitConverter.ToInt16(b, dataPos + 2 * i) / 32768f;
            }
            else if (format == 1 && bits == 24)
            {
                for (int i = 0; i < count; i++)
                {
                    int o = dataPos + 3 * i;
                    int v = (b[o] | (b[o + 1] << 8) | (b[o + 2] << 16));
                    if ((v & 0x800000) != 0) v |= unchecked((int)0xFF000000);
                    s[i] = v / 8388608f;
                }
            }
            else if (format == 1 && bits == 32)
            {
                for (int i = 0; i < count; i++) s[i] = BitConverter.ToInt32(b, dataPos + 4 * i) / 2147483648f;
            }
            else if (format == 1 && bits == 8)
            {
                for (int i = 0; i < count; i++) s[i] = (b[dataPos + i] - 128) / 128f;
            }
            else throw new FormatException(name + ": unsupported WAV format " + format + " / " + bits + " bit");
            return s;
        }
    }
}
