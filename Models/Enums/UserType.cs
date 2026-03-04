namespace UniCP.Models.Enums
{
    /// <summary>
    /// Kullanıcı tipi enum - LNGKULLANICITIPI için
    /// </summary>
    public enum UserType
    {
        /// <summary>
        /// Admin kullanıcı - Tüm firmaları görebilir
        /// </summary>
        Admin = 1,

        /// <summary>
        /// Normal müşteri - Sadece kendi firmasını görebilir
        /// </summary>
        RegularCustomer = 2,

        /// <summary>
        /// Univera dahili kullanıcı - Yetkili tüm firmaları görebilir
        /// </summary>
        UniveraInternal = 3,

        /// <summary>
        /// Univera müşteri kullanıcısı - Yetkili firmaları görebilir
        /// </summary>
        UniveraCustomer = 4
    }
}
