using QRCoder;

namespace VIHouse.WebUI.Helpers;

/// <summary>
/// Any text as an inline SVG QR code, drawn on the server. Used for the authenticator secret
/// (see AuthenticatorQr) and for referral links an ambassador can put on a slide or a card.
/// ViewBox sizing, so the CSS decides how large it renders.
/// </summary>
public static class QrSvg
{
    public static string Render(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var svg = new SvgQRCode(data);
        return svg.GetGraphic(
            pixelsPerModule: 4,
            darkColorHex: "#00230a",
            lightColorHex: "#ffffff",
            drawQuietZones: true,
            sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute);
    }
}
