namespace flex_time
{
    using Microsoft.Data.Sqlite;
    class FlexTime
    {
        static void Main(string[] args)
        {

            Console.WriteLine("flex-time:");
            var ft = new FlexTime();
            ft.run();
            Console.WriteLine("all done");
        }
        void run()
        {
          var fp=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            using (var con = new SqliteConnection()) { }
        }
    }
}