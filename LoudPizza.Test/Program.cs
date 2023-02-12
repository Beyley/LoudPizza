using System.Numerics;
using LoudPizza.Backends.OpenAL;
using LoudPizza.Core;
using LoudPizza.Modifiers;
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
				's' => throw new NotImplementedException(),
				_   => backend
			};
		}

		backend.Init();

		AudioStreamer streamer = new AudioStreamer();
		streamer.Start();

		VorbisReader reader = new VorbisReader("badapple.ogg");
		reader.Initialize();

		VorbisAudioStream   vorbisStream   = new VorbisAudioStream(reader);
		StreamedAudioStream streamedStream = new StreamedAudioStream(streamer, vorbisStream);
		streamer.RegisterStream(streamedStream);

		AudioStream stream = new AudioStream(soLoud, streamedStream);

		Handle handle = soLoud.play(stream);
		soLoud.setVolume(handle, 0.1f);
		soLoud.setLooping(handle, true);

		Console.WriteLine("Press return to exit.");
		Console.ReadLine();

		streamedStream.Dispose();

		soLoud.deinit();
	}
}
