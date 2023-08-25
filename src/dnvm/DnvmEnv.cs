
using System;
using System.IO;
using System.Threading.Tasks;
using Serde.Json;
using Zio;
using Zio.FileSystems;

namespace Dnvm;

/// <summary>
/// Represents the environment of a dnvm process.
/// <summary>
public sealed class DnvmEnv : IDisposable
{
    public const string ManifestFileName = "dnvmManifest.json";
    public static UPath ManifestPath => UPath.Root / ManifestFileName;
    public static UPath EnvPath => UPath.Root / "env";
    public static UPath DnvmExePath => UPath.Root / Utilities.DnvmExeName;

    public static DnvmEnv CreatePhysical(string realPath)
    {
        var physicalFs = new PhysicalFileSystem();
        return new DnvmEnv(
            new SubFileSystem(physicalFs, physicalFs.ConvertPathFromInternal(realPath)),
            Environment.GetEnvironmentVariable,
            Environment.SetEnvironmentVariable);
    }

    public readonly IFileSystem Vfs;
    public string RealPath(UPath path) => Vfs.ConvertPathToInternal(path);
    public SubFileSystem TempFs { get; }
    public Func<string, string?> GetUserEnvVar { get; }
    public Action<string, string> SetUserEnvVar { get; }

    public DnvmEnv(
        IFileSystem vfs,
        Func<string, string?> getUserEnvVar,
        Action<string, string> setUserEnvVar)
    {
        Vfs = vfs;
        // TempFs must be a physical file system because we pass the path to external
        // commands that will not be able to write to shared memory
        var physical = new PhysicalFileSystem();
        TempFs = new SubFileSystem(
            physical,
            physical.ConvertPathFromInternal(Path.GetTempPath()),
            owned: true);
        GetUserEnvVar = getUserEnvVar;
        SetUserEnvVar = setUserEnvVar;
    }

    /// <summary>
    /// Reads a manifest (any version) from the given path and returns
    /// an up-to-date <see cref="Manifest" /> (latest version).
    /// Throws <see cref="InvalidDataException" /> if the manifest is invalid.
    /// </summary>
    public Manifest ReadManifest()
    {
        var text = Vfs.ReadAllText(ManifestPath);
        return ManifestUtils.DeserializeNewOrOldManifest(text) ?? throw new InvalidDataException();
    }

    public void WriteManifest(Manifest manifest)
    {
        var text = JsonSerializer.Serialize(manifest);
        var tmpPath = ManifestPath + ".tmp";
        Vfs.WriteAllText(tmpPath, text);
        Vfs.MoveFile(tmpPath, ManifestPath);
    }

    public void Dispose()
    {
        Vfs.Dispose();
        TempFs.Dispose();
    }
}