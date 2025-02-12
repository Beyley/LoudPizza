using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using LoudPizza.Core;
using LoudPizza.Modifiers;

namespace LoudPizza.Sources
{
    /// <summary>
    /// Base class for audio instances.
    /// </summary>
    public abstract class AudioSourceInstance : IAudioStream, IDisposable
    {
        [Flags]
        public enum Flags
        {
            /// <summary>
            /// This audio instance loops (if supported).
            /// </summary>
            Looping = 1,

            /// <summary>
            /// This audio instance is protected - won't get stopped if we run out of voices.
            /// </summary>
            Protected = 2,

            /// <summary>
            /// This audio instance is paused.
            /// </summary>
            Paused = 4,

            /// <summary>
            /// This audio instance is affected by 3D processing.
            /// </summary>
            Process3D = 8,

            /// <summary>
            /// This audio instance has listener-relative 3D coordinates.
            /// </summary>
            ListenerRelative = 16,

            /// <summary>
            /// Currently inaudible.
            /// </summary>
            Inaudible = 32,

            /// <summary>
            /// If inaudible, should be stopped (default = don't stop).
            /// </summary>
            InaudibleStop = 64,

            /// <summary>
            /// If inaudible, should still be ticked (default = pause).
            /// </summary>
            InaudibleTick = 128,

            /// <summary>
            /// Don't auto-stop sound.
            /// </summary>
            DisableAutostop = 256
        }

        public bool IsDisposed { get; private set; }

        /// <inheritdoc/>
        public virtual uint Channels => mChannels;

        /// <inheritdoc/>
        public virtual float SampleRate => mBaseSamplerate;

        /// <inheritdoc/>
        public virtual float RelativePlaybackSpeed => mOverallRelativePlaySpeed;

        internal Stopwatch TimeInterpolationStopwatch { get; } = new Stopwatch();

        public AudioSourceInstance(AudioSource source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));

            mPlayIndex = 0;
            mFlags = 0;
            mPan = 0;
            // Default all volumes to 1.0 so sound behind N mix busses isn't super quiet.
            int i;
            for (i = 0; i < SoLoud.MaxChannels; i++)
                mChannelVolume[i] = 1.0f;
            mSetVolume = 1.0f;
            mBaseSamplerate = 44100.0f;
            mSamplerate = 44100.0f;
            mSetRelativePlaySpeed = 1.0f;
            mStreamTime = 0.0f;
            mActiveFader = 0;
            mChannels = 1;
            mBusHandle = new Handle(~0u);
            mLoopCount = 0;
            mLoopPoint = 0;
            mCurrentChannelVolume = default;
            // behind pointers because we swap between the two buffers
            mResampleData0 = -1;
            mResampleData1 = -1;
            mSrcOffset = 0;
            mLeftoverSamples = 0;
            mDelaySamples = 0;
            mOverallVolume = 0;
            mOverallRelativePlaySpeed = 1;
        }

        /// <summary>
        /// Play index; used to identify instances from handles.
        /// </summary>
        internal uint mPlayIndex;

        /// <summary>
        /// Loop count.
        /// </summary>
        internal uint mLoopCount;

        internal Flags mFlags;

        /// <summary>
        /// Pan value, for getPan().
        /// </summary>
        internal float mPan;

        /// <summary>
        /// Volume for each channel (panning).
        /// </summary>
        internal ChannelBuffer mChannelVolume;

        /// <summary>
        /// Set volume.
        /// </summary>
        internal float mSetVolume;

        /// <summary>
        /// Overall volume overall = set * 3D.
        /// </summary>
        internal float mOverallVolume;

        /// <summary>
        /// Base samplerate; samplerate = base samplerate * relative play speed.
        /// </summary>
        internal float mBaseSamplerate;

        /// <summary>
        /// Samplerate; samplerate = base samplerate * relative play speed
        /// </summary>
        internal float mSamplerate;

        /// <summary>
        /// Relative play speed; samplerate = base samplerate * relative play speed.
        /// </summary>
        internal float mSetRelativePlaySpeed;

        /// <summary>
        /// Overall relative plays peed; overall = set * 3D.
        /// </summary>
        internal float mOverallRelativePlaySpeed;

        /// <summary>
        /// How long this stream has played, in seconds.
        /// </summary>
        internal Time mStreamTime;

        /// <summary>
        /// Position of this stream, in samples.
        /// </summary>
        internal ulong mStreamPosition;

        /// <summary>
        /// Fader for the audio panning.
        /// </summary>
        internal Fader mPanFader;

        /// <summary>
        /// Fader for the audio volume.
        /// </summary>
        internal Fader mVolumeFader;

        /// <summary>
        /// Fader for the relative play speed.
        /// </summary>
        internal Fader mRelativePlaySpeedFader;

        /// <summary>
        /// Fader used to schedule pausing of the stream.
        /// </summary>
        internal Fader mPauseScheduler;

        /// <summary>
        /// Fader used to schedule stopping of the stream.
        /// </summary>
        internal Fader mStopScheduler;

        internal uint mChannels;

        /// <summary>
        /// Affected by some fader.
        /// </summary>
        internal int mActiveFader;

        /// <summary>
        /// Current channel volumes, used to ramp the volume changes to avoid clicks.
        /// </summary>
        internal ChannelBuffer mCurrentChannelVolume;

        /// <summary>
        /// The audio source that generated this instance.
        /// </summary>
        public AudioSource Source { get; }

        /// <summary>
        /// Handle of the bus this audio instance is playing on. 0 for root.
        /// </summary>
        internal Handle mBusHandle;

        /// <summary>
        /// Filters.
        /// </summary>
        private AudioFilterInstance?[]? mFilters;

        /// <summary>
        /// Initialize instance. Mostly internal use.
        /// </summary>
        public void Initialize(uint aPlayIndex)
        {
            mPlayIndex = aPlayIndex;
            mBaseSamplerate = Source.mBaseSamplerate;
            mSamplerate = mBaseSamplerate;
            mChannels = Source.mChannels;
            mStreamTime = 0.0f;
            mStreamPosition = 0;
            mLoopPoint = Source.mLoopPoint;

            AudioSource.Flags sourceFlags = Source.mFlags;
            if ((sourceFlags & AudioSource.Flags.ShouldLoop) != 0)
            {
                mFlags |= Flags.Looping;
            }
            if ((sourceFlags & AudioSource.Flags.Process3D) != 0)
            {
                mFlags |= Flags.Process3D;
            }
            if ((sourceFlags & AudioSource.Flags.ListenerRelative) != 0)
            {
                mFlags |= Flags.ListenerRelative;
            }
            if ((sourceFlags & AudioSource.Flags.InaudibleKill) != 0)
            {
                mFlags |= Flags.InaudibleStop;
            }
            if ((sourceFlags & AudioSource.Flags.InaudibleTick) != 0)
            {
                mFlags |= Flags.InaudibleTick;
            }
            if ((sourceFlags & AudioSource.Flags.DisableAutostop) != 0)
            {
                mFlags |= Flags.DisableAutostop;
            }
        }

        /// <summary>
        /// Index of the buffer for the resampler.
        /// </summary>
        internal int mResampleData0;

        /// <summary>
        /// Index of the buffer for the resampler.
        /// </summary>
        internal int mResampleData1;

        /// <summary>
        /// Sub-sample playhead; 16.16 fixed point.
        /// </summary>
        internal uint mSrcOffset;

        /// <summary>
        /// Samples left over from earlier pass.
        /// </summary>
        internal uint mLeftoverSamples;

        /// <summary>
        /// Number of samples to delay streaming.
        /// </summary>
        internal uint mDelaySamples;

        /// <summary>
        /// When looping, start playing from this sample position.
        /// </summary>
        internal ulong mLoopPoint;

        /// <inheritdoc/>
        public abstract uint GetAudio(Span<float> buffer, uint samplesToRead, uint channelStride);

        /// <inheritdoc/>
        public abstract bool HasEnded();

        /// <inheritdoc/>
        public abstract bool CanSeek();

        /// <inheritdoc/>
        public abstract SoLoudStatus Seek(ulong samplePosition, Span<float> scratch, AudioSeekFlags flags, out ulong resultPosition);

        /// <summary>
        /// Get information. Returns 0 by default.
        /// </summary>
        public virtual float GetInfo(uint infoKey)
        {
            return 0;
        }

        internal void SetFilter(int filterId, AudioFilterInstance? instance)
        {
            if (mFilters == null)
            {
                if (instance == null || IsDisposed)
                {
                    return;
                }
                mFilters = new AudioFilterInstance?[SoLoud.FiltersPerStream];
            }

            ref AudioFilterInstance? slot = ref mFilters[filterId];
            if (slot != null)
            {
                slot.Dispose();
            }
            slot = instance;
        }

        internal AudioFilterInstance? GetFilter(int filterId)
        {
            AudioFilterInstance?[]? filters = mFilters;
            if (filters != null)
            {
                return filters[filterId];
            }
            return null;
        }

        internal ReadOnlySpan<AudioFilterInstance?> GetFilters()
        {
            return mFilters.AsSpan();
        }

        internal void SwapResampleBuffers()
        {
            Debug.Assert(mResampleData0 >= 0);
            Debug.Assert(mResampleData1 >= 0);

            // Swap resample buffers (ping-pong)
            int t = mResampleData0;
            mResampleData0 = mResampleData1;
            mResampleData1 = t;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    foreach (AudioFilterInstance? instance in GetFilters())
                    {
                        if (instance != null)
                        {
                            instance.Dispose();
                        }
                    }
                    mFilters = null;
                }

                IsDisposed = true;
            }
        }

        [DoesNotReturn]
        protected void ThrowObjectDisposed()
        {
            throw new ObjectDisposedException(GetType().Name);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        ~AudioSourceInstance()
        {
            Dispose(disposing: false);
        }
    }
}
