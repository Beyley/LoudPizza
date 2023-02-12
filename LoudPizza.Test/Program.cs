using System.Diagnostics;
using LoudPizza.Backends.OpenAL;
using LoudPizza.Backends.SDL2;
using LoudPizza.Core;
using LoudPizza.Sources;
using LoudPizza.Sources.Streaming;
using LoudPizza.Test.Shared;
using LoudPizza.Vorbis;
using NVorbis;

namespace LoudPizza.Test;

public static class Program {
	public static unsafe void Main(string[] args) {
		SoLoud soLoud = new SoLoud();

		IAudioBackend? backend = null;

		while (backend == null) {
			Console.Write($"Open[A]L or [S]DL2? ");
			char input = char.ToLower(Console.ReadKey().KeyChar);
			Console.Write(Environment.NewLine);

			backend = input switch {
				'a' => new OpenALBackend(soLoud),
				's' => new SDL2Backend(soLoud),
				_   => backend
			};
		}

		long timestamp = Stopwatch.GetTimestamp();
		var  result    = backend.Init();
		timestamp = Stopwatch.GetTimestamp() - timestamp;
		Console.WriteLine($"Backend init took {timestamp / (double)Stopwatch.Frequency * 1000}ms");

		if (result != SoLoudStatus.Ok)
			throw new Exception($"Failed to initialize audio backend! reason: {result}");

		AudioStreamer streamer = new AudioStreamer();
		streamer.Start();

		// VorbisReader reader = new VorbisReader("badapple.ogg");
		// reader.Initialize();

		// VorbisAudioStream   vorbisStream   = new VorbisAudioStream(reader);
		Mp3Stream           vorbisStream   = new Mp3Stream(soLoud, File.OpenRead("badapple.mp3"), false);
		StreamedAudioStream streamedStream = new StreamedAudioStream(streamer, vorbisStream);
		streamer.RegisterStream(streamedStream);

		AudioStream stream = new AudioStream(soLoud, streamedStream);

		Handle handle = soLoud.play(stream);
		soLoud.setVolume(handle, 0.05f);
		soLoud.setLooping(handle, true);
		//Seek to 60 seconds in (48kHz * 2 channels * 60 seconds)
		soLoud.seek(handle, (ulong)(vorbisStream.SampleRate * 60), AudioSeekFlags.None);

		// while (true) {
		// 	Time streamTime = soLoud.getInterpolatedTimePosition(handle);
		// 	Console.WriteLine($"streamTime: {streamTime.Seconds * 1000d}ms");
		// 	
		// 	Thread.Sleep(1);
		// }
		
		Console.WriteLine("Press return to exit.");
		Console.ReadLine();

		streamedStream.Dispose();

		soLoud.deinit();
	}
}
