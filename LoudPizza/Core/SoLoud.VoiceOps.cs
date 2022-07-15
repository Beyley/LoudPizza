using System;
using System.Diagnostics;
using System.Threading;

namespace LoudPizza
{
    // Direct voice operations (no mutexes - called from other functions)
    public unsafe partial class SoLoud
    {
        /// <summary>
        /// Set voice (not handle) relative play speed.
        /// </summary>
        internal SoLoudStatus setVoiceRelativePlaySpeed_internal(uint aVoice, float aSpeed)
        {
            Debug.Assert(aVoice < MaxVoiceCount);
            Debug.Assert(Monitor.IsEntered(mAudioThreadMutex));

            if (aSpeed <= 0.0f)
            {
                return SoLoudStatus.InvalidParameter;
            }

            AudioSourceInstance? voice = mVoice[aVoice];
            if (voice != null)
            {
                voice.mSetRelativePlaySpeed = aSpeed;
                updateVoiceRelativePlaySpeed_internal(aVoice);
            }

            return SoLoudStatus.Ok;
        }

        /// <summary>
        /// Set voice (not handle) pause state.
        /// </summary>
        internal void setVoicePause_internal(uint aVoice, bool aPause)
        {
            Debug.Assert(aVoice < MaxVoiceCount);
            Debug.Assert(Monitor.IsEntered(mAudioThreadMutex));

            mActiveVoiceDirty = true;
            AudioSourceInstance? voice = mVoice[aVoice];
            if (voice != null)
            {
                voice.mPauseScheduler.mActive = 0;

                if (aPause)
                {
                    voice.mFlags |= AudioSourceInstance.Flags.Paused;
                }
                else
                {
                    voice.mFlags &= ~AudioSourceInstance.Flags.Paused;
                }
            }
        }

        /// <summary>
        /// Set voice (not handle) pan.
        /// </summary>
        internal void setVoicePan_internal(uint aVoice, float aPan)
        {
            Debug.Assert(aVoice < MaxVoiceCount);
            Debug.Assert(Monitor.IsEntered(mAudioThreadMutex));

            AudioSourceInstance? voice = mVoice[aVoice];
            if (voice != null)
            {
                voice.mPan = aPan;
                float l = (float)MathF.Cos((aPan + 1) * MathF.PI / 4);
                float r = (float)MathF.Sin((aPan + 1) * MathF.PI / 4);
                voice.mChannelVolume[0] = l;
                voice.mChannelVolume[1] = r;
                if (voice.mChannels == 4)
                {
                    voice.mChannelVolume[2] = l;
                    voice.mChannelVolume[3] = r;
                }
                if (voice.mChannels == 6)
                {
                    voice.mChannelVolume[2] = SQRT2RECP;
                    voice.mChannelVolume[3] = 1;
                    voice.mChannelVolume[4] = l;
                    voice.mChannelVolume[5] = r;
                }
                if (voice.mChannels == 8)
                {
                    voice.mChannelVolume[2] = SQRT2RECP;
                    voice.mChannelVolume[3] = 1;
                    voice.mChannelVolume[4] = l;
                    voice.mChannelVolume[5] = r;
                    voice.mChannelVolume[6] = l;
                    voice.mChannelVolume[7] = r;
                }
            }
        }

        /// <summary>
        /// Set voice (not handle) volume.
        /// </summary>
        internal void setVoiceVolume_internal(uint aVoice, float aVolume)
        {
            Debug.Assert(aVoice < MaxVoiceCount);
            Debug.Assert(Monitor.IsEntered(mAudioThreadMutex));

            mActiveVoiceDirty = true;
            AudioSourceInstance? voice = mVoice[aVoice];
            if (voice != null)
            {
                voice.mSetVolume = aVolume;
                updateVoiceVolume_internal(aVoice);
            }
        }

        /// <summary>
        /// Stop voice (not handle).
        /// </summary>
        internal void stopVoice_internal(uint aVoice)
        {
            Debug.Assert(aVoice < MaxVoiceCount);
            Debug.Assert(Monitor.IsEntered(mAudioThreadMutex));

            mActiveVoiceDirty = true;
            AudioSourceInstance? voice = mVoice[aVoice];
            if (voice != null)
            {
                // Delete via temporary variable to avoid recursion
                AudioSourceInstance? v = mVoice[aVoice];
                mVoice[aVoice] = null;

                for (uint i = 0; i < mMaxActiveVoices; i++)
                {
                    if (mResampleDataOwner[i] == v)
                    {
                        mResampleDataOwner[i] = null;
                    }
                }

                v?.Dispose();
            }
        }

        /// <summary>
        /// Update overall relative play speed from set and 3D speeds.
        /// </summary>
        internal void updateVoiceRelativePlaySpeed_internal(uint aVoice)
        {
            Debug.Assert(aVoice < MaxVoiceCount);
            Debug.Assert(Monitor.IsEntered(mAudioThreadMutex));

            AudioSourceInstance? voice = mVoice[aVoice];
            Debug.Assert(voice != null);

            voice.mOverallRelativePlaySpeed = m3dData[aVoice].mDopplerValue * voice.mSetRelativePlaySpeed;
            voice.mSamplerate = voice.mBaseSamplerate * voice.mOverallRelativePlaySpeed;
        }

        /// <summary>
        /// Update overall volume from set and 3D volumes.
        /// </summary>
        internal void updateVoiceVolume_internal(uint aVoice)
        {
            Debug.Assert(aVoice < MaxVoiceCount);
            Debug.Assert(Monitor.IsEntered(mAudioThreadMutex));

            AudioSourceInstance? voice = mVoice[aVoice];
            Debug.Assert(voice != null);

            voice.mOverallVolume = voice.mSetVolume * m3dData[aVoice].m3dVolume;
            if ((voice.mFlags & AudioSourceInstance.Flags.Paused) != 0)
            {
                for (int i = 0; i < MaxChannels; i++)
                {
                    voice.mCurrentChannelVolume[i] = voice.mChannelVolume[i] * voice.mOverallVolume;
                }
            }
        }
    }
}
