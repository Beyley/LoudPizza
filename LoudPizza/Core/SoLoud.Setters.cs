using System;
using System.Numerics;

namespace LoudPizza
{
    // Setters - set various bits of SoLoud state
    public unsafe partial class SoLoud
    {
        /// <summary>
        /// Set the post clip scaler value.
        /// </summary>
        public void setPostClipScaler(float aScaler)
        {
            mPostClipScaler = aScaler;
        }

        /// <summary>
        /// Set the main resampler.
        /// </summary>
        public void setMainResampler(Resampler aResampler)
        {
            if (aResampler <= Resampler.CatmullRom)
                mResampler = aResampler;
        }

        /// <summary>
        /// Set the global volume.
        /// </summary>
        public void setGlobalVolume(float aVolume)
        {
            mGlobalVolumeFader.mActive = 0;
            mGlobalVolume = aVolume;
        }

        /// <summary>
        /// Set the relative play speed.
        /// </summary>
        public SoLoudStatus setRelativePlaySpeed(Handle aVoiceHandle, float aSpeed)
        {
            lock (mAudioThreadMutex)
            {
                SoLoudStatus retVal = 0;

                ReadOnlySpan<Handle> h_ = VoiceGroupHandleToSpan(ref aVoiceHandle);
                foreach (Handle h in h_)
                {
                    int ch = getVoiceFromHandle_internal(h);
                    if (ch != -1)
                    {
                        mVoice[ch]!.mRelativePlaySpeedFader.mActive = 0;
                        retVal = setVoiceRelativePlaySpeed_internal((uint)ch, aSpeed);
                    }
                }

                return retVal;
            }
        }

        /// <summary>
        /// Set the sample rate.
        /// </summary>
        public void setSamplerate(Handle aVoiceHandle, float aSamplerate)
        {
            lock (mAudioThreadMutex)
            {
                ReadOnlySpan<Handle> h_ = VoiceGroupHandleToSpan(ref aVoiceHandle);
                foreach (Handle h in h_)
                {
                    int ch = getVoiceFromHandle_internal(h);
                    if (ch != -1)
                    {
                        mVoice[ch]!.mBaseSamplerate = aSamplerate;
                        updateVoiceRelativePlaySpeed_internal((uint)ch);
                    }
                }
            }
        }

        /// <summary>
        /// Set the pause state.
        /// </summary>
        public void setPause(Handle aVoiceHandle, bool aPause)
        {
            lock (mAudioThreadMutex)
            {
                ReadOnlySpan<Handle> h_ = VoiceGroupHandleToSpan(ref aVoiceHandle);
                foreach (Handle h in h_)
                {
                    int ch = getVoiceFromHandle_internal(h);
                    if (ch != -1)
                    {
                        setVoicePause_internal((uint)ch, aPause);
                    }
                }
            }
        }

        /// <summary>
        /// Set current maximum active voice setting.
        /// </summary>
        public SoLoudStatus setMaxActiveVoiceCount(uint aVoiceCount)
        {
            if (aVoiceCount == 0 || aVoiceCount >= MaxVoiceCount)
                return SoLoudStatus.InvalidParameter;

            lock (mAudioThreadMutex)
            {
                mMaxActiveVoices = aVoiceCount;
                initResampleData();
                mActiveVoiceDirty = true;
            }
            return SoLoudStatus.Ok;
        }

        /// <summary>
        /// Pause all voices.
        /// </summary>
        public void setPauseAll(bool aPause)
        {
            lock (mAudioThreadMutex)
            {
                for (uint ch = 0; ch < mHighestVoice; ch++)
                {
                    setVoicePause_internal(ch, aPause);
                }
            }
        }

        /// <summary>
        /// Set the voice protection state.
        /// </summary>
        public void setProtectVoice(Handle aVoiceHandle, bool aProtect)
        {
            lock (mAudioThreadMutex)
            {
                ReadOnlySpan<Handle> h_ = VoiceGroupHandleToSpan(ref aVoiceHandle);
                foreach (Handle h in h_)
                {
                    AudioSourceInstance? ch = getVoiceRefFromHandle_internal(h);
                    if (ch != null)
                    {
                        if (aProtect)
                        {
                            ch.mFlags |= AudioSourceInstance.Flags.Protected;
                        }
                        else
                        {
                            ch.mFlags &= ~AudioSourceInstance.Flags.Protected;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Set panning value; -1 is left, 0 is center, 1 is right.
        /// </summary>
        public void setPan(Handle aVoiceHandle, float aPan)
        {
            lock (mAudioThreadMutex)
            {
                ReadOnlySpan<Handle> h_ = VoiceGroupHandleToSpan(ref aVoiceHandle);
                foreach (Handle h in h_)
                {
                    int ch = getVoiceFromHandle_internal(h);
                    if (ch != -1)
                    {
                        setVoicePan_internal((uint)ch, aPan);
                    }
                }
            }
        }

        /// <summary>
        /// Set channel volume (volume for a specific speaker).
        /// </summary>
        public void setChannelVolume(Handle aVoiceHandle, uint aChannel, float aVolume)
        {
            lock (mAudioThreadMutex)
            {
                ReadOnlySpan<Handle> h_ = VoiceGroupHandleToSpan(ref aVoiceHandle);
                foreach (Handle h in h_)
                {
                    AudioSourceInstance? ch = getVoiceRefFromHandle_internal(h);
                    if (ch != null)
                    {
                        if (ch.mChannels > aChannel)
                        {
                            ch.mChannelVolume[aChannel] = aVolume;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Set absolute left/right volumes.
        /// </summary>
        public void setPanAbsolute(Handle aVoiceHandle, float aLVolume, float aRVolume)
        {
            lock (mAudioThreadMutex)
            {
                ReadOnlySpan<Handle> h_ = VoiceGroupHandleToSpan(ref aVoiceHandle);
                foreach (Handle h in h_)
                {
                    AudioSourceInstance? ch = getVoiceRefFromHandle_internal(h);
                    if (ch != null)
                    {
                        ch.mPanFader.mActive = 0;
                        ch.mChannelVolume[0] = aLVolume;
                        ch.mChannelVolume[1] = aRVolume;

                        if (ch.mChannels == 4)
                        {
                            ch.mChannelVolume[2] = aLVolume;
                            ch.mChannelVolume[3] = aRVolume;
                        }
                        else if (ch.mChannels == 6)
                        {
                            ch.mChannelVolume[2] = (aLVolume + aRVolume) * 0.5f;
                            ch.mChannelVolume[3] = (aLVolume + aRVolume) * 0.5f;
                            ch.mChannelVolume[4] = aLVolume;
                            ch.mChannelVolume[5] = aRVolume;
                        }
                        else if (ch.mChannels == 8)
                        {
                            ch.mChannelVolume[2] = (aLVolume + aRVolume) * 0.5f;
                            ch.mChannelVolume[3] = (aLVolume + aRVolume) * 0.5f;
                            ch.mChannelVolume[4] = aLVolume;
                            ch.mChannelVolume[5] = aRVolume;
                            ch.mChannelVolume[6] = aLVolume;
                            ch.mChannelVolume[7] = aRVolume;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Set behavior for inaudible sounds.
        /// </summary>
        public void setInaudibleBehavior(Handle aVoiceHandle, bool aMustTick, bool aKill)
        {
            lock (mAudioThreadMutex)
            {
                ReadOnlySpan<Handle> h_ = VoiceGroupHandleToSpan(ref aVoiceHandle);
                foreach (Handle h in h_)
                {
                    AudioSourceInstance? ch = getVoiceRefFromHandle_internal(h);
                    if (ch != null)
                    {
                        ch.mFlags &= ~(AudioSourceInstance.Flags.InaudibleKill | AudioSourceInstance.Flags.InaudibleTick);
                        if (aMustTick)
                        {
                            ch.mFlags |= AudioSourceInstance.Flags.InaudibleTick;
                        }
                        if (aKill)
                        {
                            ch.mFlags |= AudioSourceInstance.Flags.InaudibleKill;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Set voice loop point value.
        /// </summary>
        public void setLoopPoint(Handle aVoiceHandle, ulong aLoopPoint)
        {
            lock (mAudioThreadMutex)
            {
                ReadOnlySpan<Handle> h_ = VoiceGroupHandleToSpan(ref aVoiceHandle);
                foreach (Handle h in h_)
                {
                    AudioSourceInstance? ch = getVoiceRefFromHandle_internal(h);
                    if (ch != null)
                    {
                        ch.mLoopPoint = aLoopPoint;
                    }
                }
            }
        }

        /// <summary>
        /// Set voice's loop state.
        /// </summary>
        public void setLooping(Handle aVoiceHandle, bool aLooping)
        {
            lock (mAudioThreadMutex)
            {
                ReadOnlySpan<Handle> h_ = VoiceGroupHandleToSpan(ref aVoiceHandle);
                foreach (Handle h in h_)
                {
                    AudioSourceInstance? ch = getVoiceRefFromHandle_internal(h);
                    if (ch != null)
                    {
                        if (aLooping)
                        {
                            ch.mFlags |= AudioSourceInstance.Flags.Looping;
                        }
                        else
                        {
                            ch.mFlags &= ~AudioSourceInstance.Flags.Looping;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Set whether sound should auto-stop when it ends.
        /// </summary>
        public void setAutoStop(Handle aVoiceHandle, bool aAutoStop)
        {
            lock (mAudioThreadMutex)
            {
                ReadOnlySpan<Handle> h_ = VoiceGroupHandleToSpan(ref aVoiceHandle);
                foreach (Handle h in h_)
                {
                    AudioSourceInstance? ch = getVoiceRefFromHandle_internal(h);
                    if (ch != null)
                    {
                        if (aAutoStop)
                        {
                            ch.mFlags &= ~AudioSourceInstance.Flags.DisableAutostop;
                        }
                        else
                        {
                            ch.mFlags |= AudioSourceInstance.Flags.DisableAutostop;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Set overall volume.
        /// </summary>
        public void setVolume(Handle aVoiceHandle, float aVolume)
        {
            lock (mAudioThreadMutex)
            {
                ReadOnlySpan<Handle> h_ = VoiceGroupHandleToSpan(ref aVoiceHandle);
                foreach (Handle h in h_)
                {
                    int ch = getVoiceFromHandle_internal(h);
                    if (ch != -1)
                    {
                        mVoice[ch]!.mVolumeFader.mActive = 0;
                        setVoiceVolume_internal((uint)ch, aVolume);
                    }
                }
            }
        }

        /// <summary>
        /// Set delay, in samples, before starting to play samples.
        /// </summary>
        /// <remarks>
        /// Calling this on a live sound will cause glitches.
        /// </remarks>
        public void setDelaySamples(Handle aVoiceHandle, uint aSamples)
        {
            lock (mAudioThreadMutex)
            {
                ReadOnlySpan<Handle> h_ = VoiceGroupHandleToSpan(ref aVoiceHandle);
                foreach (Handle h in h_)
                {
                    AudioSourceInstance? ch = getVoiceRefFromHandle_internal(h);
                    if (ch != null)
                    {
                        ch.mDelaySamples = aSamples;
                    }
                }
            }
        }

        /// <summary>
        /// Enable or disable visualization data gathering.
        /// </summary>
        public void setVisualizationEnable(bool aEnable)
        {
            if (aEnable)
            {
                mFlags |= Flags.EnableVisualization;
            }
            else
            {
                mFlags &= ~Flags.EnableVisualization;
            }
        }

        /// <summary>
        /// Set speaker position in 3D space.
        /// </summary>
        public SoLoudStatus setSpeakerPosition(uint aChannel, Vector3 aPosition)
        {
            if (aChannel >= mChannels)
                return SoLoudStatus.InvalidParameter;

            m3dSpeakerPosition[aChannel] = aPosition;
            return SoLoudStatus.Ok;
        }
    }
}
