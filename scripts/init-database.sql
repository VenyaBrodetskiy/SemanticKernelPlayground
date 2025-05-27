IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'VectorStore')
BEGIN
  CREATE DATABASE [VectorStore];
END
GO
