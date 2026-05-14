using SelectPdf;
using System;

class Program {
    static void Main() {
        var converter = new HtmlToPdf();
        var doc = converter.ConvertHtmlString("<h1>Hello World</h1>");
        doc.Save("test.pdf");
        doc.Close();
        Console.WriteLine("PDF generated successfully.");
    }
}
