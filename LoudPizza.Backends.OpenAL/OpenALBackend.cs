using System.Runtime.InteropServices;
using LoudPizza.Core;
using LoudPizza.Test.Shared;
using Silk.NET.Core.Contexts;
using Silk.NET.OpenAL;
using Silk.NET.OpenAL.Extensions.Soft;

namespace LoudPizza.Backends.OpenAL;

// ReSharper disable once InconsistentNaming
public unsafe class OpenALBackend : IAudioBackend {
	private GCHandle _this;

	private ALContext _alc = null!;
	private AL        _al  = null!;

	private SoftCallbackBuffer _softCallbackBuffer = null!;

	private Device*      _device;
	private Context*     _context;
	private uint         _source;
	private uint         _buffer;
	private BufferFormat _bufferFormat;
	private uint         _channels;

	public OpenALBackend(SoLoud soLoud) {
		this.SoLoud = soLoud;
	}
	
	public SoLoud SoLoud {
		get;
	}
	
	public SoLoudStatus Init(uint sampleRate = 48000, uint bufferSize = 2048, uint channels = 2) {
		this._alc = ALContext.GetApi();
		this._al  = AL.GetApi();

		this._softCallbackBuffer = new SoftCallbackBuffer(new LamdaNativeContext(s => this._al.GetProcAddress(s)));

		this._device = this._alc.OpenDevice("");
		if (this._device == null) {
			return SoLoudStatus.NoAudioDevice;
		}

		this._context = this._alc.CreateContext(this._device, null);
		this._alc.MakeContextCurrent(this._context);

		AudioError err = this._al.GetError();
		if (err != AudioError.NoError) {
			switch (err) {
				case AudioError.InvalidName:
				case AudioError.IllegalEnum:
				case AudioError.InvalidValue:
				case AudioError.IllegalCommand:
					return SoLoudStatus.UnknownError;
				case AudioError.OutOfMemory:
					return SoLoudStatus.OutOfMemory;
			}
		}

		this._buffer = this._al.GenBuffer();
		
		this._this = GCHandle.Alloc(this, GCHandleType.Normal);

		switch (channels) {
			case 1:
				this._bufferFormat = BufferFormat.Mono16;
				break;
			case 2:
				this._bufferFormat = BufferFormat.Stereo16;
				break;
			default:
				return SoLoudStatus.InvalidParameter;
		}
		this._channels = channels;

		// Fill the buffer with silence
		byte[] buffer = new byte[bufferSize * channels * sizeof(ushort)];
		fixed(void* data = buffer)
			this._al.BufferData(
				this._buffer,
				this._bufferFormat,
				data,
				(int)(bufferSize * channels * sizeof(ushort)),
				(int)sampleRate
			);

		this._softCallbackBuffer.BufferCallback(
			this._buffer,
			BufferFormat.Stereo16,
			(int)sampleRate,
			new PfnBufferCallback(BufferCallback),
			(void*)GCHandle.ToIntPtr(this._this)
		);


		this.SoLoud.postinit_internal(sampleRate, bufferSize, channels);
		this.SoLoud.mBackendString      = "OpenAL";
		this.SoLoud.mBackendCleanupFunc = this.CleanupFunc;

		this._source = this._al.GenSource();
		this._al.SetSourceProperty(this._source, SourceInteger.Buffer, this._buffer);
		this._al.SourcePlay(this._source);
		
		return SoLoudStatus.Ok;
	}
	
	private void CleanupFunc(SoLoud asoloud) {
		this._this.Free();
		
		this._al.DeleteBuffer(this._buffer);
		this._al.DeleteSource(this._source);
		
		this._al.Dispose();
		this._alc.Dispose();
	}

	private static int BufferCallback(void* userPtr, void* samplerData, int numBytes) {
		GCHandle      handle = GCHandle.FromIntPtr((IntPtr)userPtr);
		OpenALBackend @this  = (OpenALBackend)handle.Target!;

		short* buf = (short*)samplerData;

		long samples = numBytes / (@this._channels * sizeof(short));
		@this.SoLoud.mixSigned16(buf, (uint)samples);

		return numBytes;
	}
}
