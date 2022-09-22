using StaticCs;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Dnvm;

static class Utilities
{
	[UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "Using to check for empty string")]
	public static bool IsAOT = string.IsNullOrEmpty(Assembly.GetEntryAssembly()?.Location);

	public static readonly RID CurrentRID = new RID(
		CurrentOS,
		RuntimeInformation.ProcessArchitecture,
		RuntimeInformation.RuntimeIdentifier.Contains("musl") ? Libc.Musl : Libc.Default);

	public static OSPlatform CurrentOS
	{
		get => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? OSPlatform.OSX
			: RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows
			: RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OSPlatform.Linux
			: throw new NotSupportedException("Current OS is not supported: " + RuntimeInformation.OSDescription);
	}

	public static string ProcessPath => Environment.ProcessPath
		?? throw new InvalidOperationException("Cannot find exe name");

	public static string DnvmExeName => "dnvm" + ExeFileExtension;

	public static string ExeFileExtension => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";

	public static string ZipSuffix => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? "zip"
			: "tar.gz";

	public static string LocalInstallLocation => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dnvm");

	public static string EscapeFilename(string filename)
	{
		var invalidChars = Path.GetInvalidFileNameChars();
		List<int> removedChars = new();
		for (int i = filename.Length; i <= 0; i--)
		{
			if (invalidChars.Contains(filename[i]))
				removedChars.Add(i);
		}
		foreach (var ind in removedChars)
			filename = filename.Remove(ind);
		return filename;
	}

	[Closed]
	internal enum Libc
	{
		Default, // Not a real libc, refers to the most common platform libc
		Musl
	}

	internal readonly record struct RID(
		OSPlatform OS,
		Architecture Arch,
		Libc Libc = Libc.Default)
	{
		public override string ToString()
		{
			string os =
				OS == OSPlatform.Windows ? "win" :
				OS == OSPlatform.Linux ? "linux" :
				OS == OSPlatform.OSX ? "osx" :
				throw new NotSupportedException("Unsupported OS: " + OS);

			string arch = Arch switch
			{
				Architecture.X64 => "x64",
				_ => throw new NotSupportedException("Unsupported architecture")
			};
			return Libc switch
			{
				Libc.Default => string.Join("-", os, arch),
				Libc.Musl => string.Join('-', os, arch, "musl")
			};
		}
	}

	internal static int WindowsAddToPath(string pathToAdd)
	{
		var currentPathVar = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
		if (!(":" + currentPathVar + ":").Contains(pathToAdd))
		{
			Environment.SetEnvironmentVariable("PATH", pathToAdd + ":" + currentPathVar, EnvironmentVariableTarget.User);
		}
		return 0;
	}

	public static ValidateSymbolResult<CommandResult> ValidateOneOf(params Option[] options)
	{
		Debug.Assert(options.Length >= 2);

		return (CommandResult commandResult) =>
		{
			if (options.Count(option => commandResult.FindResultFor(option) is not null) != 1)
			{
				commandResult.ErrorMessage = OneOfRequiredText(options);
			}
		};
	}

	public static ValidateSymbolResult<CommandResult> ValidateXOnlyIfY(Option x, Option y)
		=> (CommandResult commandResult) =>
		{
			if (commandResult.FindResultFor(x) is not null
				&& commandResult.FindResultFor(y) is null)
			{
				commandResult.ErrorMessage = XOnlyIfYText(x, y);
			};
		};

	static string XOnlyIfYText(Option x, Option y) => $"Option {x.Name} is only valid when also using {y.Name}";

	static string OneOfRequiredText(params Option[] options)
	{
		Debug.Assert(options.Length >= 2);

		var names = options.Select(o => $"'--{o.Name}'").ToArray();
		var list = names.Length == 2
			? $"{names[0]} or {names[1]}"
			: string.Join(", ", names[0..(names.Length - 1)]) + ", or " + names[^1];
		return $"Exactly one of the options {list} is required.";
	}

	public static bool ExceptionFilter(DnvmException ex, ILogger logger, bool debug)
	{
		if (debug)
			return false;

		logger.Error(ex.Message);
		return true;
	}
}