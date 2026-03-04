namespace UniCP.Constants
{
    public static class AppConstants
    {
        public static class Roles
        {
            public const string Admin = "Admin";
            public const string Finans = "Finans";
            public const string Talepler = "Talepler";
            public const string Raporlar = "Raporlar";
            public const string UniveraInternal = "UniveraInternal";
            public const string UniveraCustomer = "UniveraCustomer";
        }

        public static class Cookies
        {
            public const string SelectedCompanyId = "SelectedCompanyId";
        }

        public static class SessionKeys
        {
            // Session keys will be added here as needed
        }

        public static class DateFormats
        {
            public const string TrFormat = "dd.MM.yyyy";
        }
            
        public static class TicketStatus
        {
             public const string AnalizOnayi = "Analiz Onayı";
             public const string Tamamlandi = "Tamamlandı";
             public const string Iptal = "İptal";
        }
    }
}
