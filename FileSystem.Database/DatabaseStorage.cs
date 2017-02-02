using CKSource.FileSystem.Exceptions;

namespace FileSystem.Database
{
    using System;
    using System.Data.Entity;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;

    using CKSource.FileSystem;

    using FileInfo = CKSource.FileSystem.FileInfo;
    using Path = CKSource.FileSystem.Path;

    public class DatabaseStorage : IFileSystem
    {
        private readonly string _connectionString;

        public DatabaseStorage(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task CreateFolderAsync(string path, CancellationToken cancellationToken)
        {
            using (var context = new Context(_connectionString))
            {
                var node = new DatabaseNode
                {
                    Path = path,
                    Type = DatabaseNodeType.Folder,
                    Timestamp = DateTime.UtcNow
                };
                context.Nodes.Add(node);
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task DeleteFileAsync(string path, CancellationToken cancellationToken)
        {
            using (var context = new Context(_connectionString))
            {
                var node = await context.Nodes.FirstOrDefaultAsync(x => x.Path == path, cancellationToken);
                context.Nodes.Remove(node);
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task DeleteFolderAsync(string path, CancellationToken cancellationToken)
        {
            await DeleteFolderAsync(path, (total, current) => { }, cancellationToken);
        }

        public async Task DeleteFolderAsync(string path, Action<int, int> progressAction, CancellationToken cancellationToken)
        {
            using (var context = new Context(_connectionString))
            {
                var nodes = await context.Nodes.Where(x => x.Path.StartsWith(path) && x.Type == DatabaseNodeType.Folder).Take(1000).ToArrayAsync(cancellationToken);
                context.Nodes.RemoveRange(nodes);
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task MoveFileAsync(string srcPath, string destPath, CancellationToken cancellationToken)
        {
            using (var context = new Context(_connectionString))
            {
                var node = await context.Nodes.FirstOrDefaultAsync(x => x.Path == srcPath, cancellationToken);
                node.Path = destPath;
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task MoveFolderAsync(string srcPath, string destPath, CancellationToken cancellationToken)
        {
            await MoveFolderAsync(srcPath, destPath, (total, current) => { }, cancellationToken);
        }

        public async Task MoveFolderAsync(string srcPath, string destPath, Action<int, int> progressAction, CancellationToken cancellationToken)
        {
            using (var context = new Context(_connectionString))
            {
                var nodes = await context.Nodes.Where(x => x.Path.StartsWith(srcPath) && x.Type == DatabaseNodeType.Folder).ToArrayAsync(cancellationToken);

                var startIndex = srcPath.Length;

                foreach (var node in nodes)
                {
                    var newNode = new DatabaseNode
                    {
                        Contents = node.Contents,
                        MimeType = node.MimeType,
                        Path = Path.Combine(destPath, node.Path.Substring(startIndex)),
                        Size = node.Size,
                        Type = node.Type,
                        Timestamp = DateTime.UtcNow
                    };

                    context.Nodes.Add(newNode);
                }

                context.Nodes.RemoveRange(nodes);

                await context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task CopyFileAsync(string srcPath, string destPath, CancellationToken cancellationToken)
        {
            using (var context = new Context(_connectionString))
            {
                var node = await context.Nodes.AsNoTracking().FirstOrDefaultAsync(x => x.Path == srcPath, cancellationToken);
                node.Path = destPath;
                context.Nodes.Add(node);
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken)
        {
            using (var context = new Context(_connectionString))
            {
                var node = await context.Nodes.FirstOrDefaultAsync(x => x.Path == path, cancellationToken);
                return node != null;
            }
        }

        public async Task<bool> FolderExistsAsync(string path, CancellationToken cancellationToken)
        {
            using (var context = new Context(_connectionString))
            {
                var node = await context.Nodes.FirstOrDefaultAsync(x => x.Path == path, cancellationToken);
                return node != null;
            }
        }

        public async Task WriteAsync(Stream fileStream, string path, CancellationToken cancellationToken)
        {
            using (var context = new Context(_connectionString))
            {
                var node = await context.Nodes.FirstOrDefaultAsync(x => x.Path == path, cancellationToken);
                if (node == null)
                {
                    node = new DatabaseNode
                    {
                        Path = path,
                        Type = DatabaseNodeType.File
                    };

                    context.Nodes.Add(node);
                }

                node.Timestamp = DateTime.UtcNow;

                var buffer = new byte[16 * 1024];
                using (var ms = new MemoryStream())
                {
                    int read;
                    while ((read = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, read);
                    }

                    node.Contents = ms.ToArray();
                }

                node.Size = node.Contents.Length;
                node.MimeType = MimeMapping.GetMimeMapping(Path.GetFileName(path));

                await context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<Stream> ReadAsync(string path, CancellationToken cancellationToken)
        {
            using (var context = new Context(_connectionString))
            {
                var node = await context.Nodes.FirstOrDefaultAsync(x => x.Path == path, cancellationToken);
                return new MemoryStream(node.Contents);
            }
        }

        public async Task<FolderListResult> GetFolderInfosAsync(string path, CancellationToken cancellationToken)
        {
            using (var context = new Context(_connectionString))
            {
                var nodes = await context.Nodes.Where(x => x.Path.StartsWith(path) && x.Type == DatabaseNodeType.Folder).Take(1000).ToArrayAsync(cancellationToken);

                var startIndex = path.Length;

                var listContinuation = nodes.Length == 1000 ? new DatabaseListContinuation(path, 1000) : null;
                return
                    new FolderListResult(
                        nodes.Where(x => x.Path.Substring(startIndex).Count(y => y == '/') == 1)
                            .Select(x => new FolderInfo(Path.GetFolderName(x.Path)))
                            .ToArray(), listContinuation);
            }
        }

        public async Task<FolderListResult> GetFolderInfosAsync(IFolderListContinuation folderListContinuation, CancellationToken cancellationToken)
        {
            var listContinuation = folderListContinuation as DatabaseListContinuation;
            if (listContinuation == null)
            {
                throw new ArgumentException($"{nameof(folderListContinuation)} is invalid");
            }

            var path = listContinuation.Path;
            var skip = listContinuation.LastIndex;

            using (var context = new Context(_connectionString))
            {
                var nodes = await context.Nodes.Where(x => x.Path.StartsWith(path) && x.Type == DatabaseNodeType.Folder).Skip(skip).Take(1000).ToArrayAsync(cancellationToken);

                var startIndex = path.Length;

                listContinuation = nodes.Length == 1000 ? new DatabaseListContinuation(path, skip + 1000) : null;
                return
                    new FolderListResult(
                        nodes.Where(x => x.Path.Substring(startIndex).Count(y => y == '/') == 1)
                            .Select(x => new FolderInfo(Path.GetFolderName(x.Path)))
                            .ToArray(), listContinuation);
            }
        }

        public async Task<FileListResult> GetFileInfosAsync(string path, CancellationToken cancellationToken)
        {
            using (var context = new Context(_connectionString))
            {
                var nodes = await context.Nodes.Where(x => x.Path.StartsWith(path) && x.Type == DatabaseNodeType.File).Take(1000).ToArrayAsync(cancellationToken);

                var startIndex = path.Length;

                var listContinuation = nodes.Length == 1000 ? new DatabaseListContinuation(path, 1000) : null;
                return
                    new FileListResult(
                        nodes.Where(x => x.Path.Substring(startIndex).Count(y => y == '/') == 0)
                            .Select(x => new DatabaseFileInfo(x))
                            .ToArray(), listContinuation);
            }
        }

        public async Task<FileListResult> GetFileInfosAsync(IFileListContinuation fileListContinuation, CancellationToken cancellationToken)
        {
            var listContinuation = fileListContinuation as DatabaseListContinuation;
            if (listContinuation == null)
            {
                throw new ArgumentException($"{nameof(fileListContinuation)} is invalid");
            }

            var path = listContinuation.Path;
            var skip = listContinuation.LastIndex;

            using (var context = new Context(_connectionString))
            {
                var nodes = await context.Nodes.Where(x => x.Path.StartsWith(path) && x.Type == DatabaseNodeType.File).Skip(skip).Take(1000).ToArrayAsync(cancellationToken);

                var startIndex = path.Length;

                listContinuation = nodes.Length == 1000 ? new DatabaseListContinuation(path, skip + 1000) : null;
                return
                    new FileListResult(
                        nodes.Where(x => x.Path.Substring(startIndex).Count(y => y == '/') == 1)
                            .Select(x => new DatabaseFileInfo(x))
                            .ToArray(), listContinuation);
            }
        }

        public async Task<FolderInfo> GetFolderInfoAsync(string path, CancellationToken cancellationToken)
        {
            using (var context = new Context(_connectionString))
            {
                var node = await context.Nodes.FirstOrDefaultAsync(x => x.Path == path && x.Type == DatabaseNodeType.Folder, cancellationToken);
                if (node == null)
                {
                    throw new FolderMissingException(path);
                }

                return new FolderInfo(Path.GetFolderName(node.Path));
            }
        }

        public async Task<FileInfo> GetFileInfoAsync(string path, CancellationToken cancellationToken)
        {
            using (var context = new Context(_connectionString))
            {
                var node = await context.Nodes.FirstOrDefaultAsync(x => x.Path == path && x.Type == DatabaseNodeType.File, cancellationToken);
                if (node == null)
                {
                    throw new FileMissingException(path);
                }

                return new DatabaseFileInfo(node);
            }
        }

        public Task<string> GetUrlAsync(string path, CancellationToken cancellationToken)
        {
            // Not supported
            return Task.FromResult((string)null);
        }
    }
}
