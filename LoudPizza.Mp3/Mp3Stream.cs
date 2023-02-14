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

        public           MpegFile mpegFile;
        private readonly Stream   _stream;
        private readonly bool     _leaveOpen;

        public Mp3Stream(SoLoud soLoud, Stream stream, bool leaveOpen) {
            this._stream    = stream;
            this._leaveOpen = leaveOpen;
            
            mpegFile = new MpegFile(stream, true);
            
            // TODO: allow multiple streams from the intial stream by buffering
            // mp3Instance = new Mp3StreamInstance(this, mpegFile);
        }
        
        public void Dispose() {
            this._stream.Close();
            this.mpegFile.Dispose();
        }
        
        public uint Channels => (uint)this.mpegFile.Channels;

        public float SampleRate => this.mpegFile.SampleRate;

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
        
        public unsafe SoLoudStatus Seek(ulong samplePosition, Span<float> scratch, AudioSeekFlags flags, out ulong resultPosition) {
            this.mpegFile.Dispose();
            this.mpegFile = new MpegFile(this._stream, true);

            // this.mpegFile.Position = (long)samplePosition * sizeof(float) * this.Channels;
            this.mpegFile.Time = TimeSpan.FromSeconds(60);

            resultPosition = (ulong)(this.mpegFile.Time.TotalSeconds * this.SampleRate);
            
            return SoLoudStatus.Ok;
            // long signedSamplePosition = (long)samplePosition;
            // if (signedSamplePosition < 0)
            // {
            //     resultPosition = (ulong)this.mpegFile.Length!;
            //     return SoLoudStatus.EndOfStream;
            // }
            //
            // // TODO: bubble up exceptions?
            // try 
            // {
            //     this.mpegFile.Position = signedSamplePosition;
            //     resultPosition         = (ulong)this.mpegFile.Position;
            //     return SoLoudStatus.Ok;
            // }
            // catch (InvalidOperationException) 
            // {
            //     resultPosition = (ulong)this.mpegFile.Position;
            //     return SoLoudStatus.NotImplemented;
            // }
            // catch (ArgumentOutOfRangeException)
            // {
            //     resultPosition = (ulong)this.mpegFile.Length!;
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
