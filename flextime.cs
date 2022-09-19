namespace flex_time
{
    using Microsoft.Data.Sqlite;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.RegularExpressions;

#pragma warning disable CS7035
    class FlexTime
    {
        public enum RA { all, recent };
        public static string TF = @"yyyy-MM-dd HH:mm:ss";
        public static string AUTO = @"auto";
        public static string IN = @"in";
        public static string OUT = @"out";
        class TimeRecord
        {
            public int rowId;
            public DateTime start;
            public string status = "";
            public int subtime = 0;
            public int flextime;
        }
        private List<TimeRecord> records = new List<TimeRecord>();
        [AllowNullAttribute]
        private SqliteConnection con = null;
        private bool auto = true;
        private int subTime = 27000;
        private int recentCnt = 50;
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
            setupDb();
        }

        private void deleteRecord(TimeRecord r)
        {
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM flextime where start=$dt";
                cmd.Parameters.AddWithValue("$dt", r.start);
                cmd.ExecuteNonQuery();
            }
        }
        private void deleteRecords()
        {
            getRecords(RA.recent);
            int idx = 0;
            foreach (var r in records)
            {
                Console.WriteLine($"{idx,3} {r.start:yyyy-MM-dd HH:mm:ss} - {r.status}");
                idx += 1;
            }
            Console.Write("Enter <num> or <num>-<num> or +<num>\n" +
            " <num> - deletes a single entry\n" +
            " <num>-<num> deletes a range\n" +
            " +<num> deletes entries older than <num> days\n> ");
            var line = Console.ReadLine();
            if (line == null)
                return;
            var rx = new Regex(@"\s*\+(?<o>\d+)\s*");
            var m = rx.Matches(line);
            if (m.Count > 0)
            {
                var str = m[0].Groups[@"o"].Value;
                var days = Int32.Parse(str);
                var dt = DateTime.Now.AddDays(-days);
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = @"DELETE FROM flextime WHERE start<$d";
                    cmd.Parameters.AddWithValue(@"$d", dt);
                    cmd.ExecuteNonQuery();
                }
                return;
            }
            rx = new Regex(@"\s*(?<n1>\d+)-(?<n2>\d+)\s*");
            m = rx.Matches(line);
            if (m.Count > 0)
            {
                var gs = m[0].Groups;
                var n1 = gs[@"n1"].Value;
                var n2 = gs[@"n2"].Value;
                var n1i = Int32.Parse(n1);
                var n2i = Int32.Parse(n2);
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = @"DELETE FROM flextime WHERE start=$d";
                    for (var i = n1i; i <= n2i; ++i)
                    {
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue(@"$d", records[i].start);
                        cmd.ExecuteNonQuery();
                    }
                }
                return;
            }
            rx = new Regex(@"^\s*(?<n>\d+)\s*$");
            m = rx.Matches(line);
            if (m.Count > 0)
            {
                var n = m[0].Groups[@"n"].Value;
                var ni = Int32.Parse(n);
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = @"DELETE FROM flextime WHERE start=$d";
                    cmd.Parameters.AddWithValue(@"d", records[ni].start);
                    cmd.ExecuteNonQuery();
                }
                return;
            }
        }


        void editConfig()
        {
            var running = true;
            while (running)
            {
                Console.WriteLine();
                Console.WriteLine($"a - auto substract: {this.auto}");
                Console.WriteLine($"r - recent value to consider: {this.recentCnt}");
                Console.WriteLine($"s - substract value: {(double)this.subTime / 3600}");
                Console.WriteLine("ESC/Enter - return to main menu");
                var key = Console.ReadKey(true);
                string? value;
                switch (key.Key)
                {
                    case ConsoleKey.A:
                        this.auto = !this.auto;
                        continue;
                    case ConsoleKey.R:
                        Console.Write("Enter the number of recent records to consider each time: ");
                        value = Console.ReadLine();
                        if (value == null)
                            continue;
                        this.recentCnt = Int32.Parse(value);
                        break;
                    case ConsoleKey.S:
                        Console.Write("Enter the new value in hours (<=24) or seconds(>24): ");
                        value = Console.ReadLine();
                        if (value == null)
                            continue;
                        double dval = Double.Parse(value);
                        if (dval <= 24)
                            this.subTime = (int)(dval * 3600);
                        else
                            this.subTime = (int)(dval);
                        break;
                    case ConsoleKey.Escape:
                    case ConsoleKey.Enter:
                        running = false;
                        break;
                }
            }
            storeConfig();
        }

        void insertNewRecord(string status, int subtime)
        {
            Console.WriteLine($"Entering record: {status}");
            getRecords(RA.recent);
            var tr = new TimeRecord();
            tr.start = DateTime.Now;
            tr.status = status;
            tr.subtime = subtime;
            tr.flextime = 0;
            records.Add(tr);
            recalculate();
            getRecords(RA.recent);
        }

        private void insertRecord(TimeRecord tr)
        {
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = "INSERT OR REPLACE INTO flextime(start,status,subtime,flextime) VALUES($start,$status,$subtime,$flextime)";
                var dt = DateTime.Now;
                cmd.Parameters.AddWithValue("$start", tr.start.ToString(TF));
                cmd.Parameters.AddWithValue("$status", tr.status);
                cmd.Parameters.AddWithValue("$subtime", tr.subtime);
                cmd.Parameters.AddWithValue("$flextime", tr.flextime);
                cmd.ExecuteNonQuery();
            }
        }

        void interact()
        {
            printHelp();
            getRecords(RA.recent);
            showWorkLeft();
            while (true)
            {
                Console.Write(@"ft> ");
                var c = Console.ReadKey(true);
                Console.WriteLine();
                switch (c.Key)
                {
                    case ConsoleKey.Escape:
                    case ConsoleKey.Enter:
                        return;
                    case ConsoleKey.C:
                        editConfig();
                        break;
                    case ConsoleKey.D:
                        deleteRecords();
                        break;
                    case ConsoleKey.E:
                        editRecords();
                        break;
                    case ConsoleKey.I:
                        insertNewRecord(IN, 0);
                        showWorkLeft();
                        break;
                    case ConsoleKey.L:
                        showRecentEntries();
                        break;
                    case ConsoleKey.O:
                        insertNewRecord(OUT, 0);
                        showWorkLeft();
                        break;
                    case ConsoleKey.R:
                        getRecords(RA.all);
                        recalculate();
                        break;
                    case ConsoleKey.S:
                        singleSubstract();
                        break;
                    case ConsoleKey.W:
                        showWeekly();
                        break;
                    default:
                        printHelp();
                        break;
                }
            }
        }

        private void showWeekly()
        {
            throw new NotImplementedException();
        }

        private void singleSubstract()
        {
            throw new NotImplementedException();
        }

        private void editRecords()
        {
            getRecords(RA.recent);
            var idx = 0;
            foreach (var r in records)
            {
                Console.WriteLine($"{idx++,3}- {r.start:yyyy-MM-dd HH:mm:ss} {r.status}");
            }
            Console.Write("line to edit:>");
            var line = Console.ReadLine();
            if (line == null)
                return;
            try
            {
                idx = Int32.Parse(line);
            }
            catch { return; }
            if (idx < 0 || idx >= records.Count)
                return;
            var record = records[idx];
            Console.Write("time or status? >");
            var key = Console.ReadKey(true);
            switch (key.Key)
            {
                case ConsoleKey.T:
                    Console.WriteLine();
                    Console.WriteLine($"old: {record.start:yyyy-MM-dd HH:mm:ss}");
                    Console.Write("new: ");
                    var pos = Console.GetCursorPosition();
                    string ke = "";
                    var done = false;
                    while (!done)
                    {
                        Console.SetCursorPosition(pos.Left, pos.Top);
                        var ke2 = ke;
                        while (ke2.Length < 14)
                            ke2 = "_" + ke2;
                        ke2 = ke2.Substring(0, 4) + "-" + ke2.Substring(4, 2) + "-" + ke2.Substring(6, 2)
                            + " " + ke2.Substring(8, 2) + ":" + ke2.Substring(10, 2) + ":" + ke2.Substring(12, 2);
                        Console.Write(ke2);
                        key = Console.ReadKey(true);
                        switch (key.Key)
                        {
                            case ConsoleKey.D0:
                            case ConsoleKey.D1:
                            case ConsoleKey.D2:
                            case ConsoleKey.D3:
                            case ConsoleKey.D4:
                            case ConsoleKey.D5:
                            case ConsoleKey.D6:
                            case ConsoleKey.D7:
                            case ConsoleKey.D8:
                            case ConsoleKey.D9:
                                ke += key.KeyChar;
                                if (ke.Length > 14)
                                    ke = ke.Substring(1);
                                break;
                            case ConsoleKey.Escape:
                                return;
                            case ConsoleKey.Backspace:
                                ke = ke.Substring(0, ke.Length - 1);
                                break;
                            case ConsoleKey.Enter:
                                done = true;
                                break;
                        }
                    }
                    var od = record.start.ToString("yyyyMMddHHmmss");
                    var nd = od.Substring(0, 14 - ke.Length) + ke;
                    nd = nd.Substring(0, 4) + "-" + nd.Substring(4, 2) + "-" + nd.Substring(6, 2)
                        + " " + nd.Substring(8, 2) + ":" + nd.Substring(10, 2) + ":" + nd.Substring(12, 2);
                    Console.WriteLine();
                    Console.WriteLine(nd);
                    deleteRecord(record);
                    record.start = DateTime.Parse(nd);
                    insertRecord(record);
                    break;
                case ConsoleKey.S:
                    switch (record.status)
                    {
                        case "in":
                            record.status = "out";
                            break;
                        case "out":
                            record.status = "in";
                            break;
                    }
                    insertRecord(record);
                    break;
            }
            getRecords(RA.recent);
            recalculate();
            showWorkLeft();

        }

        void getConfig()
        {
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = @"SELECT name,val from config";
                using (var reader = cmd.ExecuteReader())
                {
                    var name = reader.GetString(0);
                    switch (name)
                    {
                        case @"auto":
                            this.auto = reader.GetBoolean(1);
                            break;
                        case @"rec":
                            this.recentCnt = reader.GetInt32(1);
                            break;
                        case @"sub":
                            this.subTime = reader.GetInt32(1);
                            break;
                        default:
                            Console.Error.WriteLine($"don't know what to do with {name}");
                            break;
                    }
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
                        cmd.CommandText = @"select ROWID, start, status, subtime, flextime from flextime order by start";
                        break;
                    default:
                        cmd.CommandText = $"select ROWID, start, status, subtime, flextime from flextime order by start desc  limit {this.recentCnt}";
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
                        tr.status = reader.GetString(2);
                        tr.subtime = reader.GetInt32(3);
                        tr.flextime = reader.GetInt32(4);
                        switch (ra)
                        {
                            case RA.all:
                                records.Add(tr);
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
c - config
d - delete records
e - edit
i - in
l - list the recent entries
o - out
r - recalculate all records
s - substract registered hours
w - show the weekly overview
ESC/Enter - exit");
        }

        private void recalculate()
        {
            TimeRecord? lastRecord = null;
            int flextime = 0;
            string status = @"unknown";
            int lastDay = 0;
            foreach (var r in records)
            {
                var save = false;
                int td = (int)r.start.ToOADate();
                if (this.auto && (r.subtime == 0) && (lastDay != td))
                {
                    r.subtime = this.subTime;
                    save = true;
                    if ((records.Count == 1) && (r.flextime == 0))
                    {
                        r.flextime = -this.subTime;
                    }
                }
                lastDay = td;
                if (lastRecord == null)
                {
                    flextime = r.flextime;
                    status = r.status;
                }
                else
                {
                    if (r.status == lastRecord.status && r.subtime == 0)
                    {
                        deleteRecord(r);
                        continue;
                    }
                    if (status == IN)
                    {
                        var diff = r.start.Subtract(lastRecord.start);
                        flextime += (int)diff.TotalSeconds - r.subtime;
                    }
                    else
                    {
                        flextime -= r.subtime;
                    }
                    if (r.flextime != flextime)
                    {
                        r.flextime = flextime;
                        save = true;
                    }
                    status = r.status;
                }
                lastRecord = r;
                if (save)
                {
                    insertRecord(r);
                }
            }
        }

        void setupDb()
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
                string[] createStmts ={
                    @"CREATE TABLE flextime(start DATETIME PRIMARY KEY, status TEXT, subtime INTEGER, flextime INTEGER)",
                    @"CREATE TABLE config(name TEXT PRIMARY KEY,  val TEXT)",
                    @"CREATE TABLE version(version INTEGER )",
                    @"INSERT INTO version(version) VALUES (1)"
                };
                foreach (var stmt in createStmts)
                {
                    cmd.CommandText = stmt;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void showRecentEntries()
        {
            getRecords(RA.recent);
            foreach (var r in records)
            {

                Console.WriteLine($"{r.start:yyyy-MM-dd HH:mm:ss} - {r.status,3} {(double)r.flextime/3600,6:#0.##}");
            }
        }

        private void showWorkLeft()
        {
            if (records.Count == 0)
                return;
            var r = records.Last();
            var ft = r.flextime;
            if (ft >= 0)
            {
                Console.WriteLine($"available flex-time: {(double)ft / 3600:0.#} hour(s)");
            }
            else if (r.status == IN)
            {
                var dt = r.start.AddSeconds(-ft);
                var dtstr = dt.ToString(TF);
                Console.WriteLine($"work until {dtstr}");
            }
            else
            {
                var t = TimeSpan.FromSeconds(-ft);
                Console.WriteLine($"do more work for {t.Hours}h {t.Minutes:00}m");
            }

        }

        void storeConfig()
        {
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = @"INSERT OR REPLACE INTO config(name, val) VALUES($name,$val)";
                // auto
                cmd.Parameters.AddWithValue(@"$name", AUTO);
                cmd.Parameters.AddWithValue(@"$val", this.auto);
                cmd.ExecuteNonQuery();
                // recentCnt
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue(@"$name", @"rec");
                cmd.Parameters.AddWithValue(@"$val", this.recentCnt);
                cmd.ExecuteNonQuery();
                // subTime
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue(@"$name", @"sub");
                cmd.Parameters.AddWithValue(@"$val", this.subTime);
                cmd.ExecuteNonQuery();
            }
        }
    }
}