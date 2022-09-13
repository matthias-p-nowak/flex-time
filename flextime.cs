namespace flex_time
{
    using Microsoft.Data.Sqlite;
    class FlexTime
    {
        static void Main(string[] args)
        {
            var version = typeof(FlexTime).Assembly.GetName().Version;
            version ??= new Version(4177, 0);
            try
            {
                Console.Clear();
                Console.WriteLine($"flex-time: {version.Major}.{version.Minor}.{version.Build}.{version.Revision}");
                var ft = new FlexTime();
                ft.run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception thrown: {ex.Message}");
            }
            Console.WriteLine("all done");
        }
        void interact(SqliteConnection con)
        {
            while (true)
            {
                var c = Console.ReadKey(false);
                var m = (c.Modifiers == ConsoleModifiers.Control ? "Ctrl" : "") + (c.Modifiers == ConsoleModifiers.Alt ? "Alt" : "") + (c.Modifiers == ConsoleModifiers.Shift ? "Shift" : "");

                Console.WriteLine($" - got key {m}-{c.Key.ToString()}");
                switch (c.Key)
                {
                    case ConsoleKey.Escape:
                    case ConsoleKey.Q:
                    case ConsoleKey.X:
                        return;
                }
            }
        }
        void run()
        {
            var fp = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dbPath = Path.Combine(fp, "flex-time.sqlite");
            var scsb = new SqliteConnectionStringBuilder();
            scsb.DataSource = dbPath;
            using (var con = new SqliteConnection(scsb.ConnectionString))
            {
                con.Open();
                updateDb(con);
                interact(con);
            }
        }
        void updateDb(SqliteConnection con)
        {
            var cur_version = 0;
            var cmd = con.CreateCommand();
            cmd.CommandText = "select max(version) from version";
            try
            {
                var obj = cmd.ExecuteScalar();
                if (obj != null)
                    cur_version = (Int32)(Int64)obj;
            }
            catch
            {
                cur_version = 0;
            }
            if (cur_version == 0)
            {
                string[] createStmts_0 ={
                    @"CREATE TABLE flextime(start DATETIME PRIMARY KEY, project TEXT) ",
                    @"CREATE TABLE version(version INTEGER )",
                    @"INSERT INTO version(version) VALUES (1)"
                };
                foreach (var stmt in createStmts_0)
                {
                    cmd.CommandText = stmt;
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}