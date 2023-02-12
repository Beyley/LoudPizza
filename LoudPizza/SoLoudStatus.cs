
namespace LoudPizza
{
    public enum SoLoudStatus
    {
        /// <summary>
        /// No error.
        /// </summary>
        Ok = 0,

        /// <summary>
        /// Some parameter is invalid.
        /// </summary>
        InvalidParameter = 1,

        /// <summary>
        /// File not found.
        /// </summary>
        FileNotFound = 2,

        /// <summary>
        /// File found, but could not be loaded.
        /// </summary>
        FileLoadFailed = 3,

        /// <summary>
        /// DLL not found, or wrong DLL.
        /// </summary>
        DllNotFound = 4,

        /// <summary>
        /// Out of memory.
        /// </summary>
        OutOfMemory = 5,

        /// <summary>
        /// Feature not implemented.
        /// </summary>
        NotImplemented = 6,

        /// <summary>
        /// Other error.
        /// </summary>
        UnknownError = 7,

        EndOfStream = 8,

        PoolExhausted = 9,
        
        /// <summary>
        /// When no audio device is found and/or no audio device succeeds in being initialized.
        /// </summary>
        NoAudioDevice = 10,
    }
}
