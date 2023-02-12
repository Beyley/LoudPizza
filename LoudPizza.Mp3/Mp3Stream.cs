using System;
using System.IO;
using LoudPizza.Core;
using LoudPizza.Sources;
using NLayer;

namespace LoudPizza
{
    public class Mp3Stream : IAudioStream
    {
        // private Mp3StreamInstance mp3Instance;

        public MpegFile mpegFile;

        public Mp3Stream(SoLoud soLoud, Stream stream, bool leaveOpen) {
            mpegFile = new MpegFile(stream, leaveOpen);
            
            this.Channels   = (uint)this.mpegFile.Channels;
            this.SampleRate = (uint)this.mpegFile.SampleRate;

            // TODO: allow multiple streams from the intial stream by buffering
            // mp3Instance = new Mp3StreamInstance(this, mpegFile);
        }
        
        public void Dispose() {
            mpegFile.Dispose();
        }
        
        public uint Channels {
            get;
        }
        
        public float SampleRate {
            get;
        }
        
        public float RelativePlaybackSpeed => 1;

        public unsafe uint GetAudio(Span<float> buffer, uint samplesToRead, uint channelStride) {
            int localBufferSize = (int)(samplesToRead * this.Channels);
            
            float*      localBuffer = stackalloc float[localBufferSize];
            Span<float> localSpan   = new Span<float>(localBuffer, localBufferSize);
            
            uint channels   = this.Channels;
            uint readTarget = samplesToRead * channels;
            if ((uint)localSpan.Length > readTarget)
                localSpan = localSpan.Slice(0, (int)readTarget);
            
            int samplesRead = this.mpegFile.ReadSamples(localSpan);
            if (samplesRead == 0)
                return 0;
            
            uint elements = (uint)(samplesRead / channels);

            //Deinterlace the data, as SoLoud wants deinterlaced data, but NLayer gives us interlaced data.
            for (uint i = 0; i < channels; i++)
            {
                for (uint j = 0; j < elements; j++) {
                    buffer[(int)(j + i * channelStride)] = localBuffer[i + j * channels];
                }
            }

            return elements;
        }
        
        public bool HasEnded() {
            return this.mpegFile.EndOfFile;
        }
        
        public bool CanSeek() {
            return this.mpegFile.CanSeek;
        }
        
        public SoLoudStatus Seek(ulong samplePosition, Span<float> scratch, AudioSeekFlags flags, out ulong resultPosition) {
            long signedSamplePosition = (long)samplePosition;
            if (signedSamplePosition < 0)
            {
                // resultPosition = (ulong)this.mpegFi?le.SampleCount;
                resultPosition = 0;
                return SoLoudStatus.EndOfStream;
            }

            // TODO: bubble up exceptions?
            // try
            // {
            this.mpegFile.Position = signedSamplePosition;
            resultPosition = (ulong)this.mpegFile.Position;
            return SoLoudStatus.Ok;
            // }
            // catch (PreRollPacketException)
            // {
            //     resultPosition = (ulong)Reader.SamplePosition;
            //     return SoLoudStatus.FileLoadFailed;
            // }
            // catch (SeekOutOfRangeException)
            // {
            //     resultPosition = (ulong)Reader.TotalSamples;
            //     return SoLoudStatus.EndOfStream;
            // }
            // catch (Exception)
            // {
            //     resultPosition = 0;
            //     return SoLoudStatus.UnknownError;
            // }
        }
    }
}
