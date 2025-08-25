// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.IO;
using PeerSync;

public class FileCache
{
	public bool IsValid()
	{
		return this.GetDirectory() != null;
	}

	public FileInfo GetFile(string hash)
	{
		DirectoryInfo? dir = this.GetDirectory();
		if (dir == null)
			throw new Exception("Missing cache directory");

		return new FileInfo(Path.Combine(dir.FullName, hash));
	}

	public void SaveFile(byte[] data, string hash)
	{
		DirectoryInfo? dir = this.GetDirectory();
		if (dir == null)
			throw new Exception("Missing cache directory");

		string path = Path.Combine(dir.FullName, hash);

		if (Path.Exists(path))
			return;

		File.WriteAllBytes(path, data);
	}

	private DirectoryInfo? GetDirectory()
	{
		if (string.IsNullOrEmpty(Configuration.Current.CacheDirectory))
			return null;

		DirectoryInfo dir;
		try
		{
			dir = new(Configuration.Current.CacheDirectory);

			if (!dir.Exists)
				dir.Create();

			return dir;
		}
		catch (Exception)
		{
		}

		return null;
	}
}