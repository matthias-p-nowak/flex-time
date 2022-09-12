namespace flex_time
{
    using Microsoft.Data.Sqlite;
    class FlexTime
    {
        private readonly string[] createStmts ={
        @"CREATE TABLE flextime(start DATETIME PRIMARY KEY,
          project TEXT) ",
        @"CREATE TABLE version(version INTEGER )",
        @"INSERT INTO version(version) VALUES (1)"

      };
        static void Main(string[] args)
        {

            Console.WriteLine("flex-time:");
            var ft = new FlexTime();
            ft.run();
            Console.WriteLine("all done");
        }
        void run()
        {
            string[] stmts = { "hello", "world" };
            var fp = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var dbPath = Path.Combine(fp, "flex-time.sqlite");
            var conStr = "Data Source=" + dbPath;
            using (var con = new SqliteConnection(conStr))
            {
                con.Open();
                var cmd = con.CreateCommand();
                foreach (var stmt in createStmts)
                {
                    Console.WriteLine(stmt);
                    cmd.CommandText = stmt;
                    cmd.ExecuteNonQuery();
                }
                con.Close();
            }
        }
    }
}