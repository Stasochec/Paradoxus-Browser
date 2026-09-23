using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using ParadoxusBrowser.Core;
using ParadoxusBrowser.Data.Models;

namespace ParadoxusBrowser.Data
{
    /// <summary>
    /// SQLite Database context managing persistent browser history, bookmarks,
    /// Speed Dial (Табло) favorites, DPAPI-encrypted user settings and saved passwords.
    /// </summary>
    public class DatabaseContext
    {
        private static readonly Lazy<DatabaseContext> _instance = new(() => new DatabaseContext());
        public static DatabaseContext Instance => _instance.Value;

        private readonly string _connectionString;

        public DatabaseContext()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string baseFolder = Path.Combine(appData, "ParadoxusBrowser");
            Directory.CreateDirectory(baseFolder);

            string dbPath = Path.Combine(baseFolder, "browser_data.db");
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            string createTablesCmd = @"
                CREATE TABLE IF NOT EXISTS History (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Url TEXT NOT NULL,
                    Title TEXT,
                    VisitedAt TEXT NOT NULL,
                    FaviconUrl TEXT
                );

                CREATE TABLE IF NOT EXISTS Bookmarks (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Url TEXT NOT NULL UNIQUE,
                    CreatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Favorites (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Url TEXT NOT NULL UNIQUE,
                    ColorHex TEXT NOT NULL,
                    IconLetter TEXT NOT NULL,
                    SortOrder INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS SecureSettings (
                    Key TEXT PRIMARY KEY,
                    EncryptedValue TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS SavedPasswords (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SiteUrl TEXT NOT NULL,
                    Username TEXT NOT NULL,
                    EncryptedPassword TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS SearchHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Query TEXT NOT NULL UNIQUE,
                    LastSearchedAt TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_SearchHistory_LastSearchedAt ON SearchHistory(LastSearchedAt DESC);

                CREATE INDEX IF NOT EXISTS IX_History_VisitedAt ON History(VisitedAt DESC);
            ";

            using var cmd = new SqliteCommand(createTablesCmd, connection);
            cmd.ExecuteNonQuery();

            // Purge legacy default favorites and corrupted favorites
            try
            {
                using var purgeLegacyCmd = new SqliteCommand(@"
                    DELETE FROM Favorites 
                    WHERE Title LIKE '%Р%' OR Title LIKE '%в–%' OR Title LIKE '%рџ%'
                       OR Url IN (
                           'https://ya.ru', 
                           'https://youtube.com', 
                           'https://web.telegram.org', 
                           'https://github.com', 
                           'https://vk.com', 
                           'https://duckduckgo.com', 
                           'https://habr.com', 
                           'https://reddit.com'
                       );
                ", connection);
                purgeLegacyCmd.ExecuteNonQuery();
            }
            catch { }

            // Seed default Speed Dial favorites if table is empty
            string checkFavoritesCmd = "SELECT COUNT(1) FROM Favorites;";
            using var countCmd = new SqliteCommand(checkFavoritesCmd, connection);
            long count = (long)(countCmd.ExecuteScalar() ?? 0L);

            if (count == 0)
            {
                string seedCmd = @"
                    INSERT INTO Favorites (Title, Url, ColorHex, IconLetter, SortOrder) VALUES
                    ('Google', 'https://www.google.com', '#4285F4', 'G', 1);
                ";
                using var seedExecuteCmd = new SqliteCommand(seedCmd, connection);
                seedExecuteCmd.ExecuteNonQuery();
            }
        }

        #region History Operations

        public async Task AddHistoryAsync(string url, string title, string faviconUrl = "")
        {
            if (string.IsNullOrWhiteSpace(url) || url.StartsWith("about:", StringComparison.OrdinalIgnoreCase) || url.StartsWith("brave:", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = @"
                    INSERT INTO History (Url, Title, VisitedAt, FaviconUrl)
                    VALUES (@url, @title, @visitedAt, @faviconUrl);
                ";

                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@url", url);
                cmd.Parameters.AddWithValue("@title", string.IsNullOrWhiteSpace(title) ? url : title);
                cmd.Parameters.AddWithValue("@visitedAt", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("@faviconUrl", faviconUrl ?? string.Empty);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding history: {ex.Message}");
            }
        }

        public async Task<List<HistoryItem>> GetHistoryAsync(int limit = 200)
        {
            var results = new List<HistoryItem>();

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = @"
                    SELECT Id, Url, Title, VisitedAt, FaviconUrl
                    FROM History
                    ORDER BY VisitedAt DESC
                    LIMIT @limit;
                ";

                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@limit", limit);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new HistoryItem
                    {
                        Id = reader.GetInt64(0),
                        Url = reader.GetString(1),
                        Title = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        VisitedAt = DateTime.TryParse(reader.GetString(3), out var dt) ? dt : DateTime.UtcNow,
                        FaviconUrl = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error retrieving history: {ex.Message}");
            }

            return results;
        }

        public async Task<bool> DeleteHistoryItemAsync(long id)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = "DELETE FROM History WHERE Id = @id;";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", id);
                int rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting history item: {ex.Message}");
                return false;
            }
        }

        public async Task ClearHistoryAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = new SqliteCommand("DELETE FROM History;", connection);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing history: {ex.Message}");
            }
        }

        #endregion

        #region Bookmarks Operations

        public async Task<bool> AddBookmarkAsync(string title, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = @"
                    INSERT OR REPLACE INTO Bookmarks (Title, Url, CreatedAt)
                    VALUES (@title, @url, @createdAt);
                ";

                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@title", string.IsNullOrWhiteSpace(title) ? url : title);
                cmd.Parameters.AddWithValue("@url", url.Trim());
                cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("o"));

                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding bookmark: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RemoveBookmarkAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = "DELETE FROM Bookmarks WHERE Url = @url;";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@url", url.Trim());

                int affected = await cmd.ExecuteNonQueryAsync();
                return affected > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing bookmark: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> IsBookmarkedAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = "SELECT COUNT(1) FROM Bookmarks WHERE Url = @url;";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@url", url.Trim());

                var count = (long?)await cmd.ExecuteScalarAsync();
                return count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking bookmark status: {ex.Message}");
                return false;
            }
        }

        public async Task<List<BookmarkItem>> GetBookmarksAsync()
        {
            var results = new List<BookmarkItem>();

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = "SELECT Id, Title, Url, CreatedAt FROM Bookmarks ORDER BY CreatedAt DESC;";
                using var cmd = new SqliteCommand(sql, connection);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new BookmarkItem
                    {
                        Id = reader.GetInt64(0),
                        Title = reader.GetString(1),
                        Url = reader.GetString(2),
                        CreatedAt = DateTime.TryParse(reader.GetString(3), out var dt) ? dt : DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error retrieving bookmarks: {ex.Message}");
            }

            return results;
        }

        #endregion

        #region Speed Dial (Табло) Favorites Operations

        public async Task<List<FavoriteItem>> GetFavoritesAsync()
        {
            var list = new List<FavoriteItem>();

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = "SELECT Id, Title, Url, ColorHex, IconLetter, SortOrder FROM Favorites ORDER BY SortOrder ASC, Id ASC;";
                using var cmd = new SqliteCommand(sql, connection);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new FavoriteItem
                    {
                        Id = reader.GetInt64(0),
                        Title = reader.GetString(1),
                        Url = reader.GetString(2),
                        ColorHex = reader.GetString(3),
                        IconLetter = reader.GetString(4),
                        SortOrder = reader.GetInt32(5)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error retrieving favorites: {ex.Message}");
            }

            return list;
        }

        public async Task<bool> AddFavoriteAsync(FavoriteItem item)
        {
            if (string.IsNullOrWhiteSpace(item.Url))
                return false;

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = @"
                    INSERT INTO Favorites (Title, Url, ColorHex, IconLetter, SortOrder)
                    VALUES (@title, @url, @color, @letter, @sort);
                    SELECT last_insert_rowid();
                ";

                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@title", string.IsNullOrWhiteSpace(item.Title) ? item.Domain : item.Title);
                cmd.Parameters.AddWithValue("@url", item.Url.Trim());
                cmd.Parameters.AddWithValue("@color", string.IsNullOrWhiteSpace(item.ColorHex) ? "#FF7600" : item.ColorHex);
                cmd.Parameters.AddWithValue("@letter", string.IsNullOrWhiteSpace(item.IconLetter) ? "★" : item.IconLetter);
                cmd.Parameters.AddWithValue("@sort", item.SortOrder);

                var insertedId = (long?)await cmd.ExecuteScalarAsync();
                if (insertedId.HasValue)
                {
                    item.Id = insertedId.Value;
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding favorite: {ex.Message}");
            }

            return false;
        }

        public async Task<bool> DeleteFavoriteAsync(long id)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = "DELETE FROM Favorites WHERE Id = @id;";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", id);

                int rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting favorite: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region DPAPI Encrypted Passwords

        public async Task<bool> AddSavedPasswordAsync(string siteUrl, string username, string plainPassword)
        {
            if (string.IsNullOrWhiteSpace(siteUrl) || string.IsNullOrWhiteSpace(username))
                return false;

            string encrypted = SecurityManager.EncryptSecret(plainPassword);

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = @"
                    INSERT INTO SavedPasswords (SiteUrl, Username, EncryptedPassword, CreatedAt)
                    VALUES (@siteUrl, @username, @encrypted, @createdAt);
                ";

                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@siteUrl", siteUrl.Trim());
                cmd.Parameters.AddWithValue("@username", username.Trim());
                cmd.Parameters.AddWithValue("@encrypted", encrypted);
                cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("o"));

                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving password: {ex.Message}");
                return false;
            }
        }

        public async Task<List<SavedPasswordItem>> GetSavedPasswordsAsync()
        {
            var results = new List<SavedPasswordItem>();

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = "SELECT Id, SiteUrl, Username, EncryptedPassword, CreatedAt FROM SavedPasswords ORDER BY CreatedAt DESC;";
                using var cmd = new SqliteCommand(sql, connection);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string enc = reader.GetString(3);
                    string plain = SecurityManager.DecryptSecret(enc);

                    results.Add(new SavedPasswordItem
                    {
                        Id = reader.GetInt64(0),
                        SiteUrl = reader.GetString(1),
                        Username = reader.GetString(2),
                        EncryptedPassword = enc,
                        PlainPassword = plain,
                        CreatedAt = DateTime.TryParse(reader.GetString(4), out var dt) ? dt : DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading saved passwords: {ex.Message}");
            }

            return results;
        }

        public async Task<bool> DeleteSavedPasswordAsync(long id)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = "DELETE FROM SavedPasswords WHERE Id = @id;";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", id);

                int rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting saved password: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region DPAPI Encrypted Settings

        public async Task SetSecureSettingAsync(string key, string plainTextValue)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            string cipherText = SecurityManager.EncryptSecret(plainTextValue);

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = @"
                    INSERT OR REPLACE INTO SecureSettings (Key, EncryptedValue)
                    VALUES (@key, @val);
                ";

                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@val", cipherText);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting secure setting: {ex.Message}");
            }
        }

        public async Task<string?> GetSecureSettingAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = "SELECT EncryptedValue FROM SecureSettings WHERE Key = @key;";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@key", key);

                var cipher = await cmd.ExecuteScalarAsync() as string;
                if (string.IsNullOrEmpty(cipher))
                    return null;

                return SecurityManager.DecryptSecret(cipher);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error retrieving secure setting: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Search History

        public async Task SaveSearchQueryAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return;

            string cleanQuery = query.Trim();
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = @"
                    INSERT INTO SearchHistory (Query, LastSearchedAt)
                    VALUES (@query, @time)
                    ON CONFLICT(Query) DO UPDATE SET LastSearchedAt = @time;
                ";

                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@query", cleanQuery);
                cmd.Parameters.AddWithValue("@time", DateTime.UtcNow.ToString("o"));

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving search query: {ex.Message}");
            }
        }

        public async Task<List<string>> GetRecentQueriesAsync(string prefix = "", int limit = 8)
        {
            var results = new List<string>();
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql;
                if (string.IsNullOrWhiteSpace(prefix))
                {
                    sql = "SELECT Query FROM SearchHistory ORDER BY LastSearchedAt DESC LIMIT @limit;";
                }
                else
                {
                    sql = "SELECT Query FROM SearchHistory WHERE Query LIKE @prefix || '%' ORDER BY LastSearchedAt DESC LIMIT @limit;";
                }

                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@limit", limit);
                if (!string.IsNullOrWhiteSpace(prefix))
                {
                    cmd.Parameters.AddWithValue("@prefix", prefix.Trim());
                }

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting recent search queries: {ex.Message}");
            }

            return results;
        }

        public async Task DeleteSearchQueryAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return;

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = "DELETE FROM SearchHistory WHERE Query = @query;";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@query", query.Trim());

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting search query: {ex.Message}");
            }
        }

        public async Task ClearSearchHistoryAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = "DELETE FROM SearchHistory;";
                using var cmd = new SqliteCommand(sql, connection);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing search history: {ex.Message}");
            }
        }

        #endregion

    }
}
