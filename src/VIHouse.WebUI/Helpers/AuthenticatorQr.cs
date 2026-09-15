using QRCoder;

namespace VIHouse.WebUI.Helpers;

/// <summary>
/// The authenticator QR code, drawn on the server as inline SVG.
///
/// It used to be drawn in the browser from a data attribute, by a library pulled in through a
/// dynamic import. That put four moving parts between the secret and the picture — a separate
/// script chunk, the service worker's cache of it, the chunk's own import of an un-fingerprinted
/// <c>main.js</c> (a second copy of the bundle in production), and the page's JS running at all —
/// and in production one of them broke and the box came up empty. Rendering here removes all four:
/// the markup arrives with the page, the same way the manual-entry key does.
///
/// The secret was already in the page as the otpauth URI, so nothing new is exposed; it still never
/// goes to a third-party image service.
/// </summary>
public static class AuthenticatorQr
{
    public static string Svg(string otpauthUri)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(otpauthUri, QRCodeGenerator.ECCLevel.M);
        var svg = new SvgQRCode(data);

        // Dark on white rather than pure black: sits with the page while staying well above the
        // contrast a scanner needs. ViewBox sizing so the CSS decides the rendered size.
        return svg.GetGraphic(
            pixelsPerModule: 4,
            darkColorHex: "#00230a",
            lightColorHex: "#ffffff",
            drawQuietZones: true,
            sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute);
    }
}
