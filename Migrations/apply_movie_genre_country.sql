-- Run this script on your KinoHub database if MovieGenres/MovieCountries are missing.
-- Migration: 20260223000000_AddMovieGenreAndMovieCountry

IF OBJECT_ID(N'[MovieGenres]', N'U') IS NULL
BEGIN
    CREATE TABLE [MovieGenres] (
        [MoviesId] int NOT NULL,
        [GenresId] int NOT NULL,
        CONSTRAINT [PK_MovieGenres] PRIMARY KEY ([MoviesId], [GenresId]),
        CONSTRAINT [FK_MovieGenres_Genres_GenresId] FOREIGN KEY ([GenresId]) REFERENCES [Genres] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MovieGenres_Movies_MoviesId] FOREIGN KEY ([MoviesId]) REFERENCES [Movies] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_MovieGenres_GenresId] ON [MovieGenres] ([GenresId]);
END
GO

IF OBJECT_ID(N'[MovieCountries]', N'U') IS NULL
BEGIN
    CREATE TABLE [MovieCountries] (
        [MoviesId] int NOT NULL,
        [CountriesId] int NOT NULL,
        CONSTRAINT [PK_MovieCountries] PRIMARY KEY ([MoviesId], [CountriesId]),
        CONSTRAINT [FK_MovieCountries_Countries_CountriesId] FOREIGN KEY ([CountriesId]) REFERENCES [Countries] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MovieCountries_Movies_MoviesId] FOREIGN KEY ([MoviesId]) REFERENCES [Movies] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_MovieCountries_CountriesId] ON [MovieCountries] ([CountriesId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260223000000_AddMovieGenreAndMovieCountry')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260223000000_AddMovieGenreAndMovieCountry', N'8.0.0');
GO
