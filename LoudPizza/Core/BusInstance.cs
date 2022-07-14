using System;

namespace LoudPizza
{
    public unsafe class BusInstance : AudioSourceInstance
    {
        protected Bus mParent;
        protected uint mScratchSize;
        protected AlignedFloatBuffer mScratch;

        /// <summary>
        /// Approximate volume for channels.
        /// </summary>
        public ChannelBuffer mVisualizationChannelVolume;

        /// <summary>
        /// Mono-mixed wave data for visualization and for visualization FFT input.
        /// </summary>
        public Buffer256 mVisualizationWaveData;

        public BusInstance(Bus aParent)
        {
            mParent = aParent;
            mFlags |= Flags.Protected | Flags.InaudibleTick;
            for (nuint i = 0; i < SoLoud.MaxChannels; i++)
                mVisualizationChannelVolume[i] = 0;
            for (nuint i = 0; i < 256; i++)
                mVisualizationWaveData[i] = 0;
            mScratchSize = SoLoud.SampleGranularity;
            mScratch.init(mScratchSize * SoLoud.MaxChannels);
        }

        public override uint getAudio(float* aBuffer, uint aSamplesToRead, uint aBufferSize)
        {
            nuint i;

            Handle handle = mParent.mChannelHandle;
            if (handle.Value == 0)
            {
                // Avoid reuse of scratch data if this bus hasn't played anything yet
                for (i = 0; i < aBufferSize * mChannels; i++)
                    aBuffer[i] = 0;
                return aSamplesToRead;
            }

            SoLoud s = mParent.mSoloud;
            s.mixBus_internal(aBuffer, aSamplesToRead, aBufferSize, mScratch.mData, handle, mSamplerate, mChannels, mParent.mResampler);

            if ((mParent.mFlags & AudioSource.Flags.VisualizationData) != 0)
            {
                for (i = 0; i < SoLoud.MaxChannels; i++)
                    mVisualizationChannelVolume[i] = 0;

                if (aSamplesToRead > 255)
                {
                    for (i = 0; i < 256; i++)
                    {
                        mVisualizationWaveData[i] = 0;
                        for (nuint j = 0; j < mChannels; j++)
                        {
                            float sample = aBuffer[i + aBufferSize * j];
                            float absvol = MathF.Abs(sample);
                            if (absvol > mVisualizationChannelVolume[j])
                                mVisualizationChannelVolume[j] = absvol;
                            mVisualizationWaveData[i] += sample;
                        }
                    }
                }
                else
                {
                    // Very unlikely failsafe branch
                    for (i = 0; i < 256; i++)
                    {
                        mVisualizationWaveData[i] = 0;
                        for (nuint j = 0; j < mChannels; j++)
                        {
                            float sample = aBuffer[(i % aSamplesToRead) + aBufferSize * j];
                            float absvol = MathF.Abs(sample);
                            if (absvol > mVisualizationChannelVolume[j])
                                mVisualizationChannelVolume[j] = absvol;
                            mVisualizationWaveData[i] += sample;
                        }
                    }
                }
            }
            return aSamplesToRead;
        }

        public override SoLoudStatus seek(ulong aSamplePosition, float* mScratch, uint mScratchSize)
        {
            return SoLoudStatus.NotImplemented;
        }

        /// <summary>
        /// Busses never stop for fear of going under 50mph.
        /// </summary>
        public override bool hasEnded()
        {
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            SoLoud s = mParent.mSoloud;

            for (uint i = 0; i < s.mHighestVoice; i++)
            {
                AudioSourceInstance? voice = s.mVoice[i];
                if (voice != null && voice.mBusHandle == mParent.mChannelHandle)
                {
                    s.stopVoice_internal(i);
                }
            }

            base.Dispose(disposing);
        }
    }
}
