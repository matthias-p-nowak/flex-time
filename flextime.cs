namespace flex_time
{
    using Microsoft.Data.Sqlite;
    using System.Collections;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;

    class FlexTime
    {
        public enum RA { all, recent };
        public static string TF = "yyyy-MM-dd HH:mm:ss";
        class TimeRecord
        {
            public int rowId;
            public DateTime start;
            public string inout = "";
            public int flextime;
        }
        List<TimeRecord> records = new List<TimeRecord>();
        [AllowNullAttribute]
        SqliteConnection con = null;
        static void Main(string[] args)
        {
            var version = typeof(FlexTime).Assembly.GetName().Version;
            version ??= new Version(4711, 0);
            try
            {
                Console.Clear();
                var sd = new DateTime(2000, 1, 1);
                sd = sd.AddDays(version.Build);
                sd = sd.AddSeconds(version.Revision * 2);
                var dstr = sd.ToString(TF);
                Console.WriteLine($"flex-time by Matthias, version: {version.Major}.{version.Minor}, compiled: {dstr}");
                var fp = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var dbPath = Path.Combine(fp, "flex-time.sqlite");
                var scsb = new SqliteConnectionStringBuilder();
                scsb.DataSource = dbPath;
                using (var con = new SqliteConnection(scsb.ConnectionString))
                {
                    if (con == null)
                    {
                        Console.WriteLine($"Could not open {dbPath}");
                        return;
                    }
                    con.Open();
                    var ft = new FlexTime(con);
                    ft.updateDb();
                    ft.interact();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception thrown: {ex.Message}");
            }
            Console.WriteLine("all done");
        }
        private FlexTime(SqliteConnection con)
        {
            // Console.WriteLine("initializing flex-time object");
            this.con = con;
        }
        void insertNewRecord(string inout)
        {
            getRecords(RA.recent);
            int flextime = 0;
            if (records.Count > 0)
            {
                // have to adjust flextime
            }
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = "insert into flextime(start,inout,flextime) values($start,$inout,$flextime)";
                var dt=DateTime.Now;
                cmd.Parameters.AddWithValue("$start", dt.ToString(TF));
                cmd.Parameters.AddWithValue("$inout", inout);
                cmd.Parameters.AddWithValue("$flextime", flextime);
                cmd.ExecuteNonQuery();
            }
            getRecords(RA.recent);
        }
        void interact()
        {
            while (true)
            {
                var c = Console.ReadKey(true);
                /*
                var m = (c.Modifiers == ConsoleModifiers.Control ? "Ctrl" : "") + (c.Modifiers == ConsoleModifiers.Alt ? "Alt" : "") + (c.Modifiers == ConsoleModifiers.Shift ? "Shift" : "");
                if (m.Length>0)
                    m+="-";
                Console.WriteLine($" - got key '{m}{c.Key.ToString()}'");
                */
                switch (c.Key)
                {
                    case ConsoleKey.Escape:
                        return;
                    case ConsoleKey.I:
                        insertNewRecord("in");
                        break;
                    case ConsoleKey.O:
                        insertNewRecord("out");
                        break;
                    default:
                        printHelp();
                        break;
                }
            }
        }
        void getRecords(RA ra)
        {
            using (var cmd = con.CreateCommand())
            {
                switch (ra)
                {
                    case RA.all:
                        cmd.CommandText = @"select ROWID, start, inout, flextime from flextime order by start";
                        break;
                    default:
                        cmd.CommandText = @"select ROWID, start, inout, flextime from flextime order by start desc  limit 20";
                        break;

                }
                using (var reader = cmd.ExecuteReader())
                {
                    records.Clear();
                    while (reader.Read())
                    {
                        var tr = new TimeRecord();
                        tr.rowId = reader.GetInt32(0);
                        tr.start = reader.GetDateTime(1);
                        tr.inout = reader.GetString(2);
                        tr.flextime = reader.GetInt32(3);
                        switch (ra)
                        {
                            case RA.all:
                                records.Append(tr);
                                break;
                            default:
                                records.Insert(0, tr);
                                break;
                        }
                    }
                }
            }
        }
        void printHelp()
        {
            Console.WriteLine(@"flex-time help:
c   - config
d   - delete old (>1 year) records
e   - edit
i   - in
l   - list the recent entries
o   - out
r   - recalculate all records
s   - substract registered hours
w   - show the weekly overview
ESC - exit
");
        }

        void updateDb()
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
                    @"CREATE TABLE flextime(start DATETIME PRIMARY KEY, inout TEXT, flextime INTEGER)",
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