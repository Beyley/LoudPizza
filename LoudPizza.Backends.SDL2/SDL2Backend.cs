using System.Runtime.InteropServices;
using LoudPizza.Core;
using LoudPizza.Test.Shared;
using SDL2;

namespace LoudPizza.Backends.SDL2;

// ReSharper disable once InconsistentNaming
public class SDL2Backend : IAudioBackend {
	private GCHandle _this;

	private SDL.SDL_AudioSpec _audioSpec;
	private uint              _audioDeviceID;

	public SoLoud SoLoud {
		get;
	}

	public SDL2Backend(SoLoud soLoud) {
		this.SoLoud = soLoud;
	}

	public SoLoudStatus Init(uint sampleRate = 48000, uint bufferSize = 2048, uint channels = 2) {
		if (SDL.SDL_WasInit(SDL.SDL_INIT_AUDIO) == 0)
			if (SDL.SDL_Init(SDL.SDL_INIT_AUDIO) != 0)
				return SoLoudStatus.UnknownError;

		SDL.SDL_AudioSpec audioSpec = new SDL.SDL_AudioSpec {
			silence  = default(byte),
			userdata = GCHandle.ToIntPtr(this._this = GCHandle.Alloc(this, GCHandleType.Normal)),
			size     = default(uint),
			freq     = (int)sampleRate,
			format   = SDL.AUDIO_F32,
			channels = (byte)channels,
			samples  = (ushort)bufferSize,
			callback = AudioCallback
		};

		const int flags = unchecked((int)(SDL.SDL_AUDIO_ALLOW_ANY_CHANGE & ~(SDL.SDL_AUDIO_ALLOW_FORMAT_CHANGE | SDL.SDL_AUDIO_ALLOW_CHANNELS_CHANGE)));
		this._audioDeviceID = SDL.SDL_OpenAudioDevice(IntPtr.Zero, 0, ref audioSpec, out this._audioSpec, flags);
		if (this._audioDeviceID == 0) {
			audioSpec.format    = SDL.AUDIO_S16;
			this._audioDeviceID = SDL.SDL_OpenAudioDevice(IntPtr.Zero, 0, ref audioSpec, out this._audioSpec, flags);
		}

		if (this._audioDeviceID == 0) {
			//If no device is available with F32 or S16, then we can't do anything
			return SoLoudStatus.NoAudioDevice;
		}

		this.SoLoud.postinit_internal((uint)this._audioSpec.freq, this._audioSpec.samples, this._audioSpec.channels);
		this.SoLoud.mBackendCleanupFunc = this.Dispose;
		this.SoLoud.mBackendString      = "SDL2";

		//Tell SDL to start playing the audio
		SDL.SDL_PauseAudioDevice(this._audioDeviceID, 0);

		return SoLoudStatus.Ok;
	}

	private void Dispose(SoLoud asoloud) {
		SDL.SDL_CloseAudioDevice(this._audioDeviceID);
		this._this.Free();
	}

	private static unsafe void AudioCallback(IntPtr userdata, IntPtr stream, int len) {
		GCHandle    handle = GCHandle.FromIntPtr(userdata);
		SDL2Backend @this  = (SDL2Backend)handle.Target!;

		short* buf = (short*)stream;
		if (@this._audioSpec.format == SDL.AUDIO_F32) {
			int samples = len / (@this._audioSpec.channels * sizeof(float));
			@this.SoLoud.mix((float*)buf, (uint)samples);
		}
		else // assume s16 if not float
		{
			int samples = len / (@this._audioSpec.channels * sizeof(short));
			@this.SoLoud.mixSigned16(buf, (uint)samples);
		}
	}
}
