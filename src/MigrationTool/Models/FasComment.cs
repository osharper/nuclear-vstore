namespace MigrationTool.Models
{
    // ReSharper disable once EnumUnderlyingTypeIsInt
    public enum FasComment : int
    {
        NewFasComment = 6,

        // Russia
        RussiaAlcohol = 0,
        RussiaSupplements = 1,
        RussiaDrugs = 3,

        // cyprus
        CyprusAlcohol = 100,
        CyprusSupplements = 101,
        CyprusDrugs = 103,
        CyprusDrugsAndService = 104,

        // czech
        CzechAlcoholAdvertising = 200,
        CzechMedsMultiple = 201,
        CzechMedsSingle = 202,
        CzechDietarySupplement = 203,
        CzechSpecialNutrition = 204,
        CzechChildNutrition = 205,
        CzechFinancilaServices = 206,
        CzechMedsTraditional = 207,
        CzechBiocides = 208,

        ChileAlcohol = 301,
        ChileDrugsAndService = 302,
        ChileMedicalReceiptDrugs = 303,

        // Были 401, 402, 403 - но теперь полностью выпилены. Использовать повторно не стоит.
        UkraineAutotherapy = 404,
        UkraineDrugs = 405,
        UkraineMedicalDevice = 406,
        UkraineAlcohol = 407,
        UkraineSoundPhonogram = 408,
        UkraineSoundLive = 409,
        UkraineEmploymentAssistance = 410,

        // Kyrgyzstan
        KyrgyzstanCertificateRequired = 701,
        KyrgyzstanAlcohol = 702,
    }
}
