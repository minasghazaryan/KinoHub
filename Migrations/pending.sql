BEGIN TRANSACTION;
GO

CREATE TABLE [FeaturedPremieres] (
    [Id] int NOT NULL IDENTITY,
    [KinopoiskId] int NOT NULL,
    [DisplayOrder] int NOT NULL,
    [NameRu] nvarchar(500) NULL,
    [NameEn] nvarchar(500) NULL,
    [PosterUrl] nvarchar(1000) NULL,
    [Year] int NULL,
    [PremiereRu] nvarchar(20) NULL,
    CONSTRAINT [PK_FeaturedPremieres] PRIMARY KEY ([Id])
);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260221000000_AddFeaturedPremieres', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [FeaturedCarousels] (
    [Id] int NOT NULL IDENTITY,
    [KinopoiskId] int NOT NULL,
    [DisplayOrder] int NOT NULL,
    [NameRu] nvarchar(500) NULL,
    [NameEn] nvarchar(500) NULL,
    [PosterUrl] nvarchar(1000) NULL,
    [ReleaseYear] nvarchar(20) NULL,
    [Rating] float NULL,
    CONSTRAINT [PK_FeaturedCarousels] PRIMARY KEY ([Id])
);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260222000000_AddFeaturedCarousels', N'8.0.0');
GO

COMMIT;
GO

