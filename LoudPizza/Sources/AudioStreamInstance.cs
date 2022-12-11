using System;
using System.Runtime.CompilerServices;

namespace LoudPizza.Sources
{
    public unsafe class AudioStreamInstance : AudioSourceInstance
    {
        public new AudioStream Source => Unsafe.As<AudioStream>(base.Source);

        /// <summary>
        /// Get the audio stream that this instance wraps around.
        /// </summary>
        public IAudioStream DataStream { get; private set; }

        /// <inheritdoc/>
        public override uint Channels => DataStream.Channels;

        /// <inheritdoc/>
        public override float SampleRate => DataStream.SampleRate;

        public AudioStreamInstance(AudioStream source, IAudioStream dataStream) : base(source)
        {
            DataStream = dataStream;
        }

        /// <inheritdoc/>
        public override uint GetAudio(Span<float> buffer, uint samplesToRead, uint channelStride)
        {
            return DataStream.GetAudio(buffer, samplesToRead, channelStride);
        }

        /// <inheritdoc/>
        public override SoLoudStatus Seek(ulong samplePosition, Span<float> scratch, out ulong resultPosition)
        {
            SoLoudStatus status = DataStream.Seek(samplePosition, scratch, out resultPosition);
            mStreamPosition = resultPosition;
            if (status == SoLoudStatus.Ok ||
                status == SoLoudStatus.EndOfStream)
            {
                mStreamPosition = resultPosition;
            }
            return status;
        }

        /// <inheritdoc/>
        public override bool HasEnded()
        {
            return DataStream.HasEnded();
        }

        /// <inheritdoc/>
        public override bool CanSeek()
        {
            return DataStream.CanSeek();
        }

        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                Source.ReturnAudioStream(this);
                DataStream = null!;
            }
            base.Dispose(disposing);
        }
    }
}
