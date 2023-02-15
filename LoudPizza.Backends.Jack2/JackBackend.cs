using System.Runtime.InteropServices;
using JackCS;
using LoudPizza.Core;
using LoudPizza.Test.Shared;

namespace LoudPizza.Backends.Jack2;

public unsafe class JackBackend : IAudioBackend {
	private Jack    _jack;
	private Client* _client;

	private Port*[] _ports;

	private GCHandle _this;
	private uint     _portCount;

	public JackBackend(SoLoud soLoud) {
		this.SoLoud = soLoud;
		this._jack = Jack.GetApi();
	}
	
	public SoLoud SoLoud {
		get;
	}
	
	public SoLoudStatus Init(uint sampleRate = 48000, uint bufferSize = 2048, uint channels = 2) {
		SoLoud.mBackendCleanupFunc = Cleanup;

		//Open the Jack client
		this._client = this._jack.ClientOpen("SoLoud", JackOptions.JackNullOption, null);
		if (this._client == null)
			return SoLoudStatus.UnknownError;

		//Force the sample rate to the one Jack uses
		sampleRate = this._jack.GetSampleRate(this._client);

		//Set the buffer size of the client
		this._jack.SetBufferSize(this._client, bufferSize);
		
		this._portCount = channels;
		
		//Create an array of all the ports
		this._ports = new Port*[channels];

		//Register all the ports
		for (int i = 0; i < channels; i++) {
			this._ports[i] = this._jack.PortRegister(this._client, $"channel_{i + 1}", Jack.DefaultAudioType, (uint)JackPortFlags.JackPortIsOutput, 0);

			if (this._ports[i] == null)
				return SoLoudStatus.UnknownError;
		}

		this._this = GCHandle.Alloc(this, GCHandleType.Normal);

		//Set the callback for audio data
		if (this._jack.SetProcessCallback(this._client, new PfnJackProcessCallback(Callback), (void*)GCHandle.ToIntPtr(this._this)) != 0)
			return SoLoudStatus.UnknownError;

		//Get all the physical audio ports
		byte** audioPorts = this._jack.GetPorts(this._client, (byte*)null, Jack.DefaultAudioType, (uint)(JackPortFlags.JackPortIsPhysical | JackPortFlags.JackPortIsInput));
		if (audioPorts == null)
			//Return an error if we cannot find any physical playback ports
			return SoLoudStatus.UnknownError;

		//Connect all the ports
		for (int i = 0; audioPorts[i] != null; i++) {
			int ret = this._jack.Connect(this._client, this._jack.PortName(this._ports[i % channels]), audioPorts[i]);
			if (ret is not 0 and not 17)
				//If there is an error and it is not EEXIST, return error
				return SoLoudStatus.UnknownError;
		}
		
		SoLoud.postinit_internal(sampleRate, bufferSize, channels);
		SoLoud.mBackendString = "JACK";

		//Activate the client, which starts Jack calling our callback
		if (this._jack.Activate(this._client) != 0)
			return SoLoudStatus.UnknownError;
		
		return SoLoudStatus.Ok;
	}
	
	private static int Callback(uint frames, void* usrData) {
		GCHandle    handle = GCHandle.FromIntPtr((IntPtr)usrData);
		JackBackend @this  = (JackBackend)handle.Target!;
		
		//Mix without copying
		@this.SoLoud.mix_without_copy(frames);
		for (uint channel = 0; channel < @this._portCount; channel++) {
			//Get the buffer for the port
			float* buf = (float*)@this._jack.PortGetBuffer(@this._ports[channel], frames);
			//Copy the de-interlaced samples of a specific channel into the buffer
			@this.SoLoud.copy_deinterlaced_channel_samples(buf, frames, channel);
		}

		return 0;
	}

	private void Cleanup(SoLoud soLoud) {
		//Close the client
		this._jack.ClientClose(this._client);
	}
}
