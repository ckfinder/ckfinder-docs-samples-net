namespace FileSystem.Database
{
    using System;

    public class DatabaseNode
    {
        public int Id { get; set; }

        public string Path { get; set; }

        public DatabaseNodeType Type { get; set; }

        public byte[] Contents { get; set; }

        public int Size { get; set; }

        public string MimeType { get; set; }

        public DateTime Timestamp { get; set; }
    }
}