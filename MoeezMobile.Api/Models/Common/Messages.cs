namespace MoeezMobile.Api.Models.Common;

/// <summary>
/// Message keys. Services, repositories and controllers only ever deal in these keys;
/// the actual Urdu or English text is chosen per request from the Accept-Language header
/// (see <see cref="Messages"/> and LocalizedResponseFilter).
/// </summary>
public static class MessageKeys
{
    // ---- generic ----
    public const string Ok = "ok.generic";
    public const string Saved = "ok.saved";
    public const string ServerError = "err.server";
    public const string ValidationFailed = "err.validation";
    public const string NotFound = "err.notFound";

    // ---- auth ----
    public const string Welcome = "ok.welcome";
    public const string BadCredentials = "err.badCredentials";
    public const string AccountDisabled = "err.accountDisabled";
    public const string SessionExpired = "err.sessionExpired";
    public const string UserExists = "err.userExists";
    public const string InvalidRole = "err.invalidRole";
    public const string CannotDisableSelf = "err.cannotDisableSelf";
    public const string UserAdded = "ok.userAdded";
    public const string UserUpdated = "ok.userUpdated";

    // ---- products ----
    public const string ProductNotFound = "err.productNotFound";
    public const string ProductNameRequired = "err.productNameRequired";
    public const string NegativePrice = "err.negativePrice";
    public const string NegativeQuantity = "err.negativeQuantity";
    public const string BarcodeInUse = "err.barcodeInUse";
    public const string ProductAdded = "ok.productAdded";
    public const string ProductUpdated = "ok.productUpdated";
    public const string ProductDeleted = "ok.productDeleted";
    public const string StockAdjusted = "ok.stockAdjusted";

    // ---- images ----
    public const string ImageEmpty = "err.imageEmpty";
    public const string ImageTooLarge = "err.imageTooLarge";      // {0} = MB
    public const string ImageWrongType = "err.imageWrongType";
    public const string ImageNotValid = "err.imageNotValid";

    // ---- purchases ----
    public const string PurchaseNotFound = "err.purchaseNotFound";
    public const string PurchaseSaved = "ok.purchaseSaved";
    public const string NoItems = "err.noItems";
    public const string ProductMissing = "err.productMissing";
    public const string CannotVoidStockSold = "err.cannotVoidStockSold";

    // ---- sales ----
    public const string SaleNotFound = "err.saleNotFound";
    public const string SaleSaved = "ok.saleSaved";
    public const string CartEmpty = "err.cartEmpty";
    public const string QuantityTooLow = "err.quantityTooLow";
    public const string InsufficientStock = "err.insufficientStock";  // {0} = product name
    public const string ProductUnavailable = "err.productUnavailable";
    public const string AlreadyVoided = "err.alreadyVoided";
    public const string InvoiceVoided = "ok.invoiceVoided";

    // ---- lookups / settings ----
    public const string NameRequired = "err.nameRequired";
    public const string BrandNotFound = "err.brandNotFound";
    public const string CategoryNotFound = "err.categoryNotFound";
    public const string SupplierNotFound = "err.supplierNotFound";
    public const string BrandSaved = "ok.brandSaved";
    public const string BrandDeleted = "ok.brandDeleted";
    public const string CategorySaved = "ok.categorySaved";
    public const string CategoryDeleted = "ok.categoryDeleted";
    public const string SupplierSaved = "ok.supplierSaved";
    public const string SupplierUpdated = "ok.supplierUpdated";
    public const string SettingsSaved = "ok.settingsSaved";

    // ---- reports ----
    public const string InvalidMonth = "err.invalidMonth";
    public const string ReportDailySales = "report.dailySales";
    public const string ReportMonthlySales = "report.monthlySales";
    public const string ReportSalesSummary = "report.salesSummary";
    public const string ReportPurchaseSummary = "report.purchaseSummary";
    public const string ReportProfitByProduct = "report.profitByProduct";
    public const string ReportCurrentStock = "report.currentStock";
    public const string ReportLowStock = "report.lowStock";
    public const string ReportStockHistory = "report.stockHistory";
}

/// <summary>Two-language catalogue. Urdu is the default; English is served when the client asks for it.</summary>
public static class Messages
{
    public const string Urdu = "ur";
    public const string English = "en";

    private static readonly Dictionary<string, (string Ur, string En)> Catalog = new(StringComparer.Ordinal)
    {
        [MessageKeys.Ok] = ("کامیاب", "Success"),
        [MessageKeys.Saved] = ("کامیابی سے محفوظ ہو گیا", "Saved successfully"),
        [MessageKeys.ServerError] = ("سرور میں خرابی پیش آ گئی۔ دوبارہ کوشش کریں۔", "A server error occurred. Please try again."),
        [MessageKeys.ValidationFailed] = ("درج کردہ معلومات درست نہیں ہیں", "The information provided is not valid"),
        [MessageKeys.NotFound] = ("ریکارڈ نہیں ملا", "Record not found"),

        [MessageKeys.Welcome] = ("خوش آمدید", "Welcome"),
        [MessageKeys.BadCredentials] = ("یوزر نیم یا پاس ورڈ غلط ہے", "Incorrect username or password"),
        [MessageKeys.AccountDisabled] = ("یہ اکاؤنٹ غیر فعال ہے", "This account is disabled"),
        [MessageKeys.SessionExpired] = ("سیشن ختم ہو گیا", "Your session has expired"),
        [MessageKeys.UserExists] = ("یہ یوزر نیم پہلے سے موجود ہے", "That username already exists"),
        [MessageKeys.InvalidRole] = ("رول صرف Admin یا Salesman ہو سکتا ہے", "Role must be either Admin or Salesman"),
        [MessageKeys.CannotDisableSelf] = ("آپ اپنا اکاؤنٹ غیر فعال نہیں کر سکتے", "You cannot disable your own account"),
        [MessageKeys.UserAdded] = ("یوزر شامل ہو گیا", "User added"),
        [MessageKeys.UserUpdated] = ("یوزر اپ ڈیٹ ہو گیا", "User updated"),

        [MessageKeys.ProductNotFound] = ("پروڈکٹ نہیں ملی", "Product not found"),
        [MessageKeys.ProductNameRequired] = ("پروڈکٹ کا نام درج کریں", "Enter a product name"),
        [MessageKeys.NegativePrice] = ("قیمت منفی نہیں ہو سکتی", "Price cannot be negative"),
        [MessageKeys.NegativeQuantity] = ("مقدار منفی نہیں ہو سکتی", "Quantity cannot be negative"),
        [MessageKeys.BarcodeInUse] = ("یہ بارکوڈ پہلے سے کسی اور پروڈکٹ پر موجود ہے", "That barcode is already used by another product"),
        [MessageKeys.ProductAdded] = ("پروڈکٹ کامیابی سے شامل ہو گئی", "Product added successfully"),
        [MessageKeys.ProductUpdated] = ("پروڈکٹ اپ ڈیٹ ہو گئی", "Product updated"),
        [MessageKeys.ProductDeleted] = ("پروڈکٹ حذف کر دی گئی", "Product deleted"),
        [MessageKeys.StockAdjusted] = ("اسٹاک درست کر دیا گیا", "Stock corrected"),

        [MessageKeys.ImageEmpty] = ("تصویر خالی ہے", "The image is empty"),
        [MessageKeys.ImageTooLarge] = ("تصویر کا سائز {0} MB سے کم ہونا چاہیے", "The image must be smaller than {0} MB"),
        [MessageKeys.ImageWrongType] = ("صرف jpg، png یا webp تصویر قبول کی جاتی ہے", "Only jpg, png or webp images are accepted"),
        [MessageKeys.ImageNotValid] = ("فائل درست تصویر نہیں ہے", "That file is not a valid image"),

        [MessageKeys.PurchaseNotFound] = ("خریداری کا بل نہیں ملا", "Purchase invoice not found"),
        [MessageKeys.PurchaseSaved] = ("خریداری محفوظ ہو گئی اور اسٹاک بڑھا دیا گیا", "Purchase saved and stock increased"),
        [MessageKeys.NoItems] = ("کم از کم ایک آئٹم شامل کریں", "Add at least one item"),
        [MessageKeys.ProductMissing] = ("ایک یا زیادہ پروڈکٹس موجود نہیں ہیں", "One or more products do not exist"),
        [MessageKeys.CannotVoidStockSold] = (
            "یہ بل منسوخ نہیں ہو سکتا کیونکہ اسٹاک پہلے ہی فروخت ہو چکا ہے",
            "This invoice cannot be voided because the stock has already been sold"),

        [MessageKeys.SaleNotFound] = ("فروخت کا بل نہیں ملا", "Sale invoice not found"),
        [MessageKeys.SaleSaved] = ("فروخت محفوظ ہو گئی", "Sale saved"),
        [MessageKeys.CartEmpty] = ("کارٹ خالی ہے۔ کم از کم ایک آئٹم شامل کریں۔", "The cart is empty. Add at least one item."),
        [MessageKeys.QuantityTooLow] = ("مقدار صفر سے زیادہ ہونی چاہیے", "Quantity must be greater than zero"),
        [MessageKeys.InsufficientStock] = (
            "اسٹاک ناکافی ہے۔ دستیاب مقدار چیک کریں۔ ({0})",
            "Insufficient stock. Please check the available quantity. ({0})"),
        [MessageKeys.ProductUnavailable] = ("پروڈکٹ موجود نہیں ہے", "That product does not exist"),
        [MessageKeys.AlreadyVoided] = ("یہ بل پہلے ہی منسوخ ہو چکا ہے", "This invoice has already been voided"),
        [MessageKeys.InvoiceVoided] = ("بل منسوخ کر دیا گیا اور اسٹاک واپس کر دیا گیا", "Invoice voided and stock restored"),

        [MessageKeys.NameRequired] = ("نام خالی نہیں ہو سکتا", "Name cannot be empty"),
        [MessageKeys.BrandNotFound] = ("برانڈ نہیں ملا", "Brand not found"),
        [MessageKeys.CategoryNotFound] = ("کیٹیگری نہیں ملی", "Category not found"),
        [MessageKeys.SupplierNotFound] = ("سپلائر نہیں ملا", "Supplier not found"),
        [MessageKeys.BrandSaved] = ("برانڈ محفوظ ہو گیا", "Brand saved"),
        [MessageKeys.BrandDeleted] = ("برانڈ حذف کر دیا گیا", "Brand deleted"),
        [MessageKeys.CategorySaved] = ("کیٹیگری محفوظ ہو گئی", "Category saved"),
        [MessageKeys.CategoryDeleted] = ("کیٹیگری حذف کر دی گئی", "Category deleted"),
        [MessageKeys.SupplierSaved] = ("سپلائر محفوظ ہو گیا", "Supplier saved"),
        [MessageKeys.SupplierUpdated] = ("سپلائر اپ ڈیٹ ہو گیا", "Supplier updated"),
        [MessageKeys.SettingsSaved] = ("سیٹنگز محفوظ ہو گئیں", "Settings saved"),

        [MessageKeys.InvalidMonth] = ("مہینہ 1 سے 12 کے درمیان ہونا چاہیے", "Month must be between 1 and 12"),
        [MessageKeys.ReportDailySales] = ("روزانہ سیل رپورٹ", "Daily Sales Report"),
        [MessageKeys.ReportMonthlySales] = ("ماہانہ سیل رپورٹ", "Monthly Sales Report"),
        [MessageKeys.ReportSalesSummary] = ("کل فروخت رپورٹ", "Sales Summary Report"),
        [MessageKeys.ReportPurchaseSummary] = ("کل خریداری رپورٹ", "Purchase Summary Report"),
        [MessageKeys.ReportProfitByProduct] = ("پروڈکٹ کے حساب سے پرافٹ", "Profit by Product"),
        [MessageKeys.ReportCurrentStock] = ("موجودہ اسٹاک رپورٹ", "Current Stock Report"),
        [MessageKeys.ReportLowStock] = ("کم اسٹاک رپورٹ", "Low Stock Report"),
        [MessageKeys.ReportStockHistory] = ("مکمل اسٹاک ہسٹری", "Full Stock History"),
    };

    // ---- enum labels (also rendered client-side; kept here for Excel exports) ----

    public static string SaleTypeLabel(byte saleType, string language) => saleType switch
    {
        2 => language == English ? "Wholesale" : "ہول سیل",
        _ => language == English ? "Retail" : "ریٹیل"
    };

    public static string PaymentLabel(byte method, string language) => method switch
    {
        2 => language == English ? "Easypaisa / JazzCash" : "ایزی پیسہ / جاز کیش",
        3 => language == English ? "Credit" : "ادھار",
        _ => language == English ? "Cash" : "نقد"
    };

    public static string TxnTypeLabel(byte txnType, string language) => txnType switch
    {
        1 => language == English ? "Purchase" : "خریداری",
        2 => language == English ? "Sale" : "فروخت",
        3 => language == English ? "Adjustment" : "درستگی",
        4 => language == English ? "Purchase Return" : "خریداری واپسی",
        5 => language == English ? "Sale Return" : "فروخت واپسی",
        _ => language == English ? "Unknown" : "نامعلوم"
    };

    /// <summary>Normalises an Accept-Language header down to "ur" or "en". Urdu is the default.</summary>
    public static string Normalize(string? acceptLanguage)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguage)) return Urdu;
        return acceptLanguage.TrimStart().StartsWith("en", StringComparison.OrdinalIgnoreCase) ? English : Urdu;
    }

    public static bool IsKnownKey(string? value) => value is not null && Catalog.ContainsKey(value);

    /// <summary>
    /// Translates a key. Anything that isn't a known key is returned unchanged, so free text
    /// (a product name, a validation message from a data annotation) still passes through.
    /// </summary>
    public static string Resolve(string? key, string language, params object[] args)
    {
        if (key is null) return string.Empty;
        if (!Catalog.TryGetValue(key, out var pair)) return key;

        var text = language == English ? pair.En : pair.Ur;
        return args.Length == 0 ? text : string.Format(text, args);
    }
}
