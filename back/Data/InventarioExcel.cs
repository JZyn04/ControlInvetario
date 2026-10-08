using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace back.Data;

// Exportación del servidor: OOXML sin fórmulas, macros, enlaces externos ni dependencias de escritorio.
public static class InventarioExcel
{
    public sealed record Hoja(string Nombre, IReadOnlyList<string> Encabezados,
        IReadOnlyList<object?[]> Filas, IReadOnlyList<double>? Anchos = null);
    public sealed record Dinero(decimal Valor);

    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace C = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string RelBase = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    public static byte[] Crear(params Hoja[] hojas)
    {
        if (hojas.Length == 0 || hojas.Select(h => h.Nombre).Distinct(StringComparer.OrdinalIgnoreCase).Count() != hojas.Length)
            throw new ArgumentException("El libro necesita hojas con nombres únicos.", nameof(hojas));
        foreach (var hoja in hojas)
        {
            if (string.IsNullOrWhiteSpace(hoja.Nombre) || hoja.Nombre.Length > 31 || hoja.Nombre.IndexOfAny(['[', ']', ':', '*', '?', '/', '\\']) >= 0 ||
                hoja.Encabezados.Count is 0 or > 16384 || hoja.Filas.Count > 1048575 ||
                hoja.Filas.Any(f => f.Length != hoja.Encabezados.Count))
                throw new ArgumentException("La estructura de la hoja no es válida.", nameof(hojas));
        }
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            Escribir(zip, "[Content_Types].xml", new XElement(C + "Types",
                new XElement(C + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(C + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(C + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(C + "Override", new XAttribute("PartName", "/xl/styles.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml")),
                hojas.Select((_, i) => new XElement(C + "Override", new XAttribute("PartName", $"/xl/worksheets/sheet{i + 1}.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));
            Escribir(zip, "_rels/.rels", new XElement(P + "Relationships",
                Relacion("rId1", "officeDocument", "xl/workbook.xml")));
            Escribir(zip, "xl/workbook.xml", new XElement(S + "workbook", new XAttribute(XNamespace.Xmlns + "r", R),
                new XElement(S + "bookViews", new XElement(S + "workbookView")),
                new XElement(S + "sheets", hojas.Select((h, i) => new XElement(S + "sheet", new XAttribute("name", h.Nombre),
                    new XAttribute("sheetId", i + 1), new XAttribute(R + "id", $"rId{i + 1}"))))));
            Escribir(zip, "xl/_rels/workbook.xml.rels", new XElement(P + "Relationships",
                hojas.Select((_, i) => Relacion($"rId{i + 1}", "worksheet", $"worksheets/sheet{i + 1}.xml")),
                Relacion($"rId{hojas.Length + 1}", "styles", "styles.xml")));
            Escribir(zip, "xl/styles.xml", Estilos());
            for (var i = 0; i < hojas.Length; i++) EscribirHoja(zip, $"xl/worksheets/sheet{i + 1}.xml", hojas[i]);
        }
        return memoria.ToArray();
    }

    private static XElement Relacion(string id, string tipo, string destino) => new(P + "Relationship",
        new XAttribute("Id", id), new XAttribute("Type", RelBase + tipo), new XAttribute("Target", destino));

    private static void Escribir(ZipArchive zip, string ruta, XElement documento)
    {
        using var stream = zip.CreateEntry(ruta, CompressionLevel.Fastest).Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false });
        writer.WriteStartDocument(true);
        documento.WriteTo(writer);
        writer.WriteEndDocument();
    }

    private static XElement Estilos() => new(S + "styleSheet",
        new XElement(S + "numFmts", new XAttribute("count", 3),
            new XElement(S + "numFmt", new XAttribute("numFmtId", 164), new XAttribute("formatCode", "yyyy-mm-dd hh:mm:ss \"UTC\"")),
            new XElement(S + "numFmt", new XAttribute("numFmtId", 165), new XAttribute("formatCode", "#,##0.000")),
            new XElement(S + "numFmt", new XAttribute("numFmtId", 166), new XAttribute("formatCode", "#,##0.00"))),
        new XElement(S + "fonts", new XAttribute("count", 2),
            new XElement(S + "font", new XElement(S + "sz", new XAttribute("val", 11)), new XElement(S + "name", new XAttribute("val", "Arial"))),
            new XElement(S + "font", new XElement(S + "b"), new XElement(S + "sz", new XAttribute("val", 11)),
                new XElement(S + "color", new XAttribute("rgb", "FFFFFFFF")), new XElement(S + "name", new XAttribute("val", "Arial")))),
        new XElement(S + "fills", new XAttribute("count", 3),
            new XElement(S + "fill", new XElement(S + "patternFill", new XAttribute("patternType", "none"))),
            new XElement(S + "fill", new XElement(S + "patternFill", new XAttribute("patternType", "gray125"))),
            new XElement(S + "fill", new XElement(S + "patternFill", new XAttribute("patternType", "solid"),
                new XElement(S + "fgColor", new XAttribute("rgb", "FF24465A")), new XElement(S + "bgColor", new XAttribute("indexed", 64))))),
        new XElement(S + "borders", new XAttribute("count", 1), new XElement(S + "border",
            new XElement(S + "left"), new XElement(S + "right"), new XElement(S + "top"), new XElement(S + "bottom"), new XElement(S + "diagonal"))),
        new XElement(S + "cellStyleXfs", new XAttribute("count", 1), Formato(0, 0, 0)),
        new XElement(S + "cellXfs", new XAttribute("count", 6), Formato(0, 0, 0), Formato(0, 1, 2),
            Formato(165, 0, 0), Formato(164, 0, 0), Formato(166, 0, 0), Formato(3, 0, 0)),
        new XElement(S + "cellStyles", new XAttribute("count", 1), new XElement(S + "cellStyle", new XAttribute("name", "Normal"),
            new XAttribute("xfId", 0), new XAttribute("builtinId", 0))));

    private static XElement Formato(int numero, int fuente, int relleno) => new(S + "xf", new XAttribute("numFmtId", numero),
        new XAttribute("fontId", fuente), new XAttribute("fillId", relleno), new XAttribute("borderId", 0), new XAttribute("xfId", 0),
        new XAttribute("applyNumberFormat", 1), new XAttribute("applyFont", 1), new XAttribute("applyFill", 1), new XAttribute("applyAlignment", 1),
        new XElement(S + "alignment", new XAttribute("vertical", "center"), fuente == 1 ? new XAttribute("horizontal", "center") : null, new XAttribute("wrapText", 1)));

    private static void EscribirHoja(ZipArchive zip, string ruta, Hoja hoja)
    {
        using var stream = zip.CreateEntry(ruta, CompressionLevel.Fastest).Open();
        using var w = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false });
        w.WriteStartDocument(true);
        w.WriteStartElement("worksheet", S.NamespaceName);
        var rango = $"A1:{Columna(hoja.Encabezados.Count)}{hoja.Filas.Count + 1}";
        new XElement(S + "dimension", new XAttribute("ref", rango)).WriteTo(w);
        new XElement(S + "sheetViews", new XElement(S + "sheetView", new XAttribute("workbookViewId", 0), new XAttribute("showGridLines", 0),
            new XElement(S + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"),
                new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")),
            new XElement(S + "selection", new XAttribute("pane", "bottomLeft"), new XAttribute("activeCell", "A2"), new XAttribute("sqref", "A2")))).WriteTo(w);
        new XElement(S + "sheetFormatPr", new XAttribute("defaultRowHeight", 30)).WriteTo(w);
        new XElement(S + "cols", Enumerable.Range(0, hoja.Encabezados.Count).Select(i => new XElement(S + "col",
            new XAttribute("min", i + 1), new XAttribute("max", i + 1),
            new XAttribute("width", Math.Clamp(hoja.Anchos is not null && i < hoja.Anchos.Count ? hoja.Anchos[i] : 20, 8, 80)),
            new XAttribute("customWidth", 1)))).WriteTo(w);
        w.WriteStartElement("sheetData", S.NamespaceName);
        Fila(w, 1, hoja.Encabezados.Select(s => (object?)s).ToArray(), hoja.Anchos, cabecera: true);
        for (var i = 0; i < hoja.Filas.Count; i++) Fila(w, i + 2, hoja.Filas[i], hoja.Anchos, cabecera: false);
        w.WriteEndElement();
        new XElement(S + "autoFilter", new XAttribute("ref", rango)).WriteTo(w);
        w.WriteEndElement();
        w.WriteEndDocument();
    }

    private static void Fila(XmlWriter w, int numero, object?[] valores, IReadOnlyList<double>? anchos, bool cabecera)
    {
        w.WriteStartElement("row", S.NamespaceName);
        w.WriteAttributeString("r", numero.ToString(Invariante));
        var lineas = valores.Select((v, i) => v is string texto ? TextoSeguro(texto).Split('\n').Sum(linea =>
            Math.Max(1, (int)Math.Ceiling(linea.Length / Math.Max(4, (anchos is not null && i < anchos.Count ? anchos[i] : 20) * 0.8)))) : 1).DefaultIfEmpty(1).Max();
        w.WriteAttributeString("ht", Math.Min(409, Math.Max(cabecera ? 32 : 22, lineas * 16 + 4)).ToString(Invariante));
        w.WriteAttributeString("customHeight", "1");
        for (var i = 0; i < valores.Length; i++)
        {
            w.WriteStartElement("c", S.NamespaceName);
            w.WriteAttributeString("r", Columna(i + 1) + numero.ToString(Invariante));
            var valor = valores[i];
            var estilo = cabecera ? 1 : valor is DateTime ? 3 : valor is Dinero ? 4 : EsNumero(valor)
                ? Convert.ToDecimal(valor, Invariante) == decimal.Truncate(Convert.ToDecimal(valor, Invariante)) ? 5 : 2 : 0;
            w.WriteAttributeString("s", estilo.ToString(Invariante));
            if (valor is DateTime fecha)
            {
                var utc = fecha.Kind == DateTimeKind.Local ? fecha.ToUniversalTime() : fecha;
                w.WriteElementString("v", S.NamespaceName, utc.ToOADate().ToString("R", Invariante));
            }
            else if (valor is Dinero dinero && EsDecimalSeguro(dinero.Valor))
                w.WriteElementString("v", S.NamespaceName, dinero.Valor.ToString(Invariante));
            else if (EsNumero(valor))
                w.WriteElementString("v", S.NamespaceName, Convert.ToString(valor, Invariante));
            else
            {
                w.WriteAttributeString("t", "inlineStr");
                w.WriteStartElement("is", S.NamespaceName);
                w.WriteStartElement("t", S.NamespaceName);
                w.WriteAttributeString("xml", "space", XNamespace.Xml.NamespaceName, "preserve");
                var texto = valor is Dinero d ? d.Valor.ToString(Invariante) : Convert.ToString(valor, Invariante) ?? string.Empty;
                w.WriteString(TextoSeguro(texto)); // Un texto que empiece por =, +, - o @ nunca se convierte en fórmula.
                w.WriteEndElement();
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static bool EsNumero(object? valor) => valor switch
    {
        decimal numero => EsDecimalSeguro(numero),
        byte or sbyte or short or ushort or int or uint => true,
        long numero => numero is >= -999999999999999 and <= 999999999999999,
        ulong numero => numero <= 999999999999999,
        _ => false
    };

    // Excel conserva 15 dígitos: los números mayores se escriben como texto para no perder datos.
    private static bool EsDecimalSeguro(decimal numero) => numero.ToString("G29", Invariante)
        .Where(char.IsDigit).SkipWhile(c => c == '0').Count() <= 15;

    private static string TextoSeguro(string texto)
    {
        var limpio = new StringBuilder(Math.Min(texto.Length, 32767));
        foreach (var rune in texto.EnumerateRunes())
        {
            if (rune.Value is not (0x9 or 0xA or 0xD) && (rune.Value < 0x20 || rune.Value is 0xFFFE or 0xFFFF)) continue;
            if (limpio.Length + rune.Utf16SequenceLength > 32767) break;
            limpio.Append(rune.ToString());
        }
        return limpio.ToString();
    }

    private static string Columna(int numero)
    {
        var texto = string.Empty;
        while (numero > 0) { numero--; texto = (char)('A' + numero % 26) + texto; numero /= 26; }
        return texto;
    }
}
