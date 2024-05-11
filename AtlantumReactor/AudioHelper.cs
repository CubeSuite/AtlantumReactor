using AtlantumReactor;
using System;
using UnityEngine;

public static class AudioHelper
{
    public static AudioClip WAVToAudioClip(byte[] wavBytes, string clipName) {
        if (wavBytes.Length < 44) // Minimum header size for WAV files
        {
            AtlantumReactorPlugin.Log.LogError("Invalid WAV file: Header too short.");
            return null;
        }

        int offset = 0;
        // "RIFF" marker
        string riff = System.Text.Encoding.ASCII.GetString(wavBytes, offset, 4);
        offset += 4;
        if (riff != "RIFF") {
            AtlantumReactorPlugin.Log.LogError("Invalid WAV file: No RIFF header.");
            return null;
        }

        // Skip over chunk size
        offset += 4;

        // "WAVE" format
        string wave = System.Text.Encoding.ASCII.GetString(wavBytes, offset, 4);
        offset += 4;
        if (wave != "WAVE") {
            AtlantumReactorPlugin.Log.LogError("Invalid WAV file: No WAVE header.");
            return null;
        }

        // Process chunks
        bool foundFormat = false;
        bool foundData = false;
        int sampleRate = 0;
        int numChannels = 0;
        int bitsPerSample = 0;
        float[] audioData = null;
        while (offset < wavBytes.Length) {
            string chunkId = System.Text.Encoding.ASCII.GetString(wavBytes, offset, 4);
            int chunkSize = BitConverter.ToInt32(wavBytes, offset + 4);
            offset += 8;

            if (chunkId == "fmt ") {
                foundFormat = true;
                ushort audioFormat = BitConverter.ToUInt16(wavBytes, offset);
                if (audioFormat != 1) // PCM = 1
                {
                    AtlantumReactorPlugin.Log.LogError("Invalid WAV file: Only PCM format is supported.");
                    return null;
                }

                numChannels = BitConverter.ToUInt16(wavBytes, offset + 2);
                sampleRate = BitConverter.ToInt32(wavBytes, offset + 4);
                // Byte rate and block align not needed, skip
                bitsPerSample = BitConverter.ToUInt16(wavBytes, offset + 14);
                offset += chunkSize;
            }
            else if (chunkId.StartsWith("data") || chunkId == "datapr") {
                foundData = true;
                int dataLength = chunkSize / (bitsPerSample / 8);
                audioData = new float[dataLength];

                for (int i = 0; i < dataLength; i++) {
                    if (bitsPerSample == 16) {
                        short sample = BitConverter.ToInt16(wavBytes, offset);
                        audioData[i] = sample / 32768f; // Convert to [-1,1] range
                        offset += 2;
                    }
                    else if (bitsPerSample == 8) {
                        audioData[i] = (wavBytes[offset] - 128) / 128f; // Convert to [-1,1] range
                        offset += 1;
                    }
                }
                break; // Exit loop after finding the data
            }
            else {
                // Skip unknown or irrelevant chunks
                offset += chunkSize;
            }
        }

        if (!foundFormat || !foundData || audioData == null) {
            AtlantumReactorPlugin.Log.LogError("Failed to find necessary WAV header information.");
            return null;
        }

        AudioClip audioClip = AudioClip.Create(clipName, audioData.Length, numChannels, sampleRate, false);
        audioClip.SetData(audioData, 0);
        return audioClip;
    }
}