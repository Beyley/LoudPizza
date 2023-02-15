using System.Diagnostics;
using LoudPizza.Core;
using LoudPizza.Test.Shared;

namespace LoudPizza.Backends.Null;

public class NullBackend : IAudioBackend {
	public NullBackend(SoLoud soLoud) {
		this.SoLoud = soLoud;
	}
	
	public SoLoud SoLoud {
		get;
	}

	private bool   _run = true;
	private Thread _thread;
	public unsafe SoLoudStatus Init(uint sampleRate = 48000, uint bufferSize = 2048, uint channels = 2) {
		SoLoud.postinit_internal(sampleRate, bufferSize, channels);
		SoLoud.mBackendString      = "Null";
		SoLoud.mBackendCleanupFunc = this.Cleanup;

		this._thread = new Thread(() => {
			Stopwatch stopwatch = Stopwatch.StartNew();
			float*    buffer    = stackalloc float[(int)(bufferSize * channels)];
			while (this._run) {
				//Mix to a scratch buffer
				this.SoLoud.mix(buffer, bufferSize);

				TimeSpan timeToSleep = TimeSpan.FromSeconds((double)bufferSize / sampleRate);
				
				TimeSpan before = stopwatch.Elapsed;
				//Sleep for the duration of the buffer
				Thread.Sleep(timeToSleep);
				
				//spin for the remainder of the time
				SpinWait.SpinUntil(() => {
					TimeSpan now = stopwatch.Elapsed;
					return now - before >= timeToSleep;
				});
			}
		});
		this._thread.Start();
		
		return SoLoudStatus.Ok;
	}
	
	private void Cleanup(SoLoud asoloud) {
		this._run = false;
	}
}
