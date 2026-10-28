namespace HackerToolbox.Services
{
    // Apenas exibição: o instante armazenado nunca é alterado.
    // Regra: DateTimeKind.Utc é convertido; Unspecified (SQLite não guarda Kind) é tratado como UTC,
    // que é como os registros novos são gravados. Registros antigos do SQLite (horário local) ainda
    // não têm regra de conversão definida (veja docs/DATABASE.md). Local nunca é esperado vindo do banco.
    public static class BrasiliaTime
    {
        private static readonly TimeZoneInfo Zone = Resolve();

        private static TimeZoneInfo Resolve()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
        }

        public static DateTime ToBrasilia(DateTime value)
        {
            var utc = value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
            return TimeZoneInfo.ConvertTimeFromUtc(utc, Zone);
        }

        public static string Format(DateTime value) => ToBrasilia(value).ToString("dd/MM/yyyy HH:mm");
    }
}
