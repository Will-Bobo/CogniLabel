namespace CogniLabel.Infrastructure.IO;

public interface IFileSystemService
{
    void CreateDirectory(string path);
    void CopyFile(string sourcePath, string destPath, bool overwrite);
    bool FileExists(string path);
    bool DirectoryExists(string path);
}

