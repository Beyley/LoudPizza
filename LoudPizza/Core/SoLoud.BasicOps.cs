using System;

namespace LoudPizza
{
    public unsafe partial class SoLoud
    {
        /// <summary>
        /// Start playing a sound. 
        /// Returns voice handle, which can be ignored or used to alter the playing sound's parameters. 
        /// Negative volume means to use default.
        /// </summary>
        public Handle play(AudioSource aSound, float aVolume = -1.0f, float aPan = 0.0f, bool aPaused = false, Handle aBus = default)
        {
            if ((aSound.mFlags & AudioSource.FLAGS.SINGLE_INSTANCE) != 0)
            {
                // Only one instance allowed, stop others
                aSound.stop();
            }

            // Creation of an audio instance may take significant amount of time,
            // so let's not do it inside the audio thread mutex.
            aSound.mSoloud = this;
            AudioSourceInstance instance = aSound.createInstance();

            lock (mAudioThreadMutex)
            {
                int ch = findFreeVoice_internal();
                if (ch < 0)
                {
                    instance.Dispose();
                    return new Handle((uint)SOLOUD_ERRORS.UNKNOWN_ERROR);
                }
                if (aSound.mAudioSourceID == 0)
                {
                    aSound.mAudioSourceID = mAudioSourceID;
                    mAudioSourceID++;
                }
                mVoice[ch] = instance;
                instance.mAudioSourceID = aSound.mAudioSourceID;
                instance.mBusHandle = aBus;
                instance.init(aSound, mPlayIndex);
                m3dData[ch].init(aSound);

                mPlayIndex++;

                // 20 bits, skip the last one (top bits full = voice group)
                if (mPlayIndex == 0xfffff)
                {
                    mPlayIndex = 0;
                }

                if (aPaused)
                {
                    instance.mFlags |= AudioSourceInstance.FLAGS.PAUSED;
                }

                setVoicePan_internal((uint)ch, aPan);
                if (aVolume < 0)
                {
                    setVoiceVolume_internal((uint)ch, aSound.mVolume);
                }
                else
                {
                    setVoiceVolume_internal((uint)ch, aVolume);
                }

                // Fix initial voice volume ramp up		
                int i;
                for (i = 0; i < MAX_CHANNELS; i++)
                {
                    instance.mCurrentChannelVolume[i] = instance.mChannelVolume[i] * instance.mOverallVolume;
                }

                setVoiceRelativePlaySpeed_internal((uint)ch, 1);

                for (i = 0; i < FILTERS_PER_STREAM; i++)
                {
                    Filter? filter = aSound.mFilter[i];
                    if (filter != null)
                    {
                        instance.mFilter[i] = filter.createInstance();
                    }
                }

                mActiveVoiceDirty = true;

                Handle handle = getHandleFromVoice_internal((uint)ch);
                return handle;
            }
        }

        /// <summary>
        /// Start playing a sound delayed in relation to other sounds called via this function. 
        /// Negative volume means to use default.
        /// </summary>
        public Handle playClocked(Time aSoundTime, AudioSource aSound, float aVolume = -1.0f, float aPan = 0.0f, Handle aBus = default)
        {
            Handle h = play(aSound, aVolume, aPan, true, aBus);
            Time lasttime;
            lock (mAudioThreadMutex)
            {
                // mLastClockedTime is cleared to zero at start of every output buffer
                lasttime = mLastClockedTime;
                if (lasttime == 0)
                {
                    mLastClockedTime = aSoundTime;
                    lasttime = aSoundTime;
                }
            }
            int samples = (int)Math.Floor((aSoundTime - lasttime) * mSamplerate);
            // Make sure we don't delay too much (or overflow)
            if (samples < 0 || samples > 2048)
                samples = 0;
            setDelaySamples(h, (uint)samples);
            setPause(h, false);
            return h;
        }

        /// <summary>
        /// Start playing a sound without any panning. It will be played at full volume.
        /// </summary>
        public Handle playBackground(AudioSource aSound, float aVolume = -1.0f, bool aPaused = false, Handle aBus = default)
        {
            Handle h = play(aSound, aVolume, 0.0f, aPaused, aBus);
            setPanAbsolute(h, 1.0f, 1.0f);
            return h;
        }

        /// <summary>
        /// Seek the audio stream to certain point in time.
        /// </summary>
        /// <remarks>
        /// Some streams can't seek backwards. 
        /// Relative play speed affects time.
        /// </remarks>
        public SOLOUD_ERRORS seek(Handle aVoiceHandle, ulong aSamplePosition)
        {
            SOLOUD_ERRORS res = SOLOUD_ERRORS.SO_NO_ERROR;

            void body(Handle h)
            {
                AudioSourceInstance? ch = getVoiceRefFromHandle_internal(h);
                if (ch != null)
                {
                    SOLOUD_ERRORS singleres = ch.seek(aSamplePosition, mScratch.mData, mScratchSize);
                    if (singleres != SOLOUD_ERRORS.SO_NO_ERROR)
                        res = singleres;
                }
            }

            lock (mAudioThreadMutex)
            {
                ArraySegment<Handle> h_ = voiceGroupHandleToArray_internal(aVoiceHandle);
                if (h_.Array == null)
                {
                    body(aVoiceHandle);
                }
                else
                {
                    foreach (Handle h in h_.AsSpan())
                        body(h);
                }
            }
            return res;
        }

        /// <summary>
        /// Stop the sound.
        /// </summary>
        public void stop(Handle aVoiceHandle)
        {
            void body(Handle h)
            {
                int ch = getVoiceFromHandle_internal(h);
                if (ch != -1)
                {
                    stopVoice_internal((uint)ch);
                }
            }

            lock (mAudioThreadMutex)
            {
                ArraySegment<Handle> h_ = voiceGroupHandleToArray_internal(aVoiceHandle);
                if (h_.Array == null)
                {
                    body(aVoiceHandle);
                }
                else
                {
                    foreach (Handle h in h_.AsSpan())
                        body(h);
                }
            }
        }

        /// <summary>
        /// Stop all voices that play this sound source.
        /// </summary>
        public void stopAudioSource(AudioSource aSound)
        {
            if (aSound.mAudioSourceID != 0)
            {
                lock (mAudioThreadMutex)
                {
                    for (uint i = 0; i < mHighestVoice; i++)
                    {
                        AudioSourceInstance? voice = mVoice[i];
                        if (voice != null && voice.mAudioSourceID == aSound.mAudioSourceID)
                        {
                            stopVoice_internal(i);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Stop all voices.
        /// </summary>
        public void stopAll()
        {
            lock (mAudioThreadMutex)
            {
                for (uint i = 0; i < mHighestVoice; i++)
                {
                    stopVoice_internal(i);
                }
            }
        }

        /// <summary>
        /// Gets the amount of voices that play this audio source.
        /// </summary>
        public int countAudioSource(AudioSource aSound)
        {
            int count = 0;
            if (aSound.mAudioSourceID != 0)
            {
                lock (mAudioThreadMutex)
                {
                    for (uint i = 0; i < mHighestVoice; i++)
                    {
                        AudioSourceInstance? voice = mVoice[i];
                        if (voice != null && voice.mAudioSourceID == aSound.mAudioSourceID)
                        {
                            count++;
                        }
                    }
                }
            }
            return count;
        }
    }
}
