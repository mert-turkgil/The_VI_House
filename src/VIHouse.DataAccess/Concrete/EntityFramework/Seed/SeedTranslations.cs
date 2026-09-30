using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using VIHouse.Entities.Content;
using VIHouse.Entities.Journal;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Seed;

/// <summary>
/// German, Turkish and Estonian for the seeded homepage sections and the seeded journal posts.
///
/// Safe in every environment, including Production: a translation is only added where
///   - the block or post still carries exactly the seeded English (so text an admin has rewritten
///     is never "translated" into something that no longer matches it), and
///   - that language has no row yet (so nothing an admin translated by hand is overwritten).
/// It never creates a block or a post. Anything it skips can be translated in Admin > Content or
/// Admin > Journal. The Estonian is a first draft for a native speaker to review.
/// </summary>
public static class SeedTranslations
{
    private record BlockCopy(string? Heading = null, string? Subheading = null, string? BodyText = null, string? CtaLabel = null, string? ExtraJson = null);

    /// <summary>The seeded English each translation belongs to: section key → heading + list.</summary>
    private static readonly Dictionary<string, (string? Heading, string? ExtraJson)> EnglishBlocks = new()
    {
        ["hero"] = ("Where ambition meets alignment.", null),
        ["feature-strip"] = ("Find what matters to you", """[{"label":"Learn","description":"From founders and operators who have built what you are building."},{"label":"Connect","description":"Meet people beyond your existing circle."},{"label":"Grow","description":"Access frameworks and strategies to scale."},{"label":"Experience","description":"Join gatherings that create lasting relationships."},{"label":"Belong","description":"The House continues beyond the event."}]"""),
        ["ecosystem"] = ("More than retreats. A complete network.", """[{"title":"Live & On-Demand Webinars","description":"Learn from top founders, investors and experts across business, finance, marketing and personal growth.","imageUrl":"/img/ecosystem/webinars-800.jpg","imageAlt":"A speaker on stage in front of a seated audience","linkLabel":"Browse Webinars","linkUrl":"/sessions"},{"title":"Community & Networking","description":"Join private groups, meet like-minded peers and collaborate on the projects that matter.","imageUrl":"/img/ecosystem/community-800.jpg","imageAlt":"A group talking in a bright open workspace","linkLabel":"Enter Community","linkUrl":"/membership"},{"title":"Digital Marketplace","description":"Discover and purchase high-quality business programmes, templates, resources and tools.","imageUrl":"/img/ecosystem/marketplace-800.jpg","imageAlt":"A laptop and notebook on a desk by a window","linkLabel":"Browse Marketplace","linkUrl":"/experiences"},{"title":"Signature Retreats","description":"Join transformative retreats in world-class locations that elevate your mind, network and business.","imageUrl":"/img/ecosystem/retreats-800.jpg","imageAlt":"A villa terrace and pool at sunset","linkLabel":"View Retreats","linkUrl":"/experiences"}]"""),
        ["stats"] = (null, """[{"value":"180+","label":"Members"},{"value":"24","label":"Countries"},{"value":"65+","label":"Experiences"},{"value":"40+","label":"Experts"}]"""),
        ["trust"] = ("Serious builders find their people here.", """[{"quote":"The connections I made at VI House changed my business and my life.","author":"Placeholder Member","role":"E-commerce Founder","avatarUrl":"/img/people/voice-a-800.jpg"},{"quote":"Best community of high-level operators I have ever been part of.","author":"Placeholder Member","role":"Digital Creator","avatarUrl":"/img/people/voice-b-800.jpg"},{"quote":"The retreats are unmatched. Pure transformation.","author":"Placeholder Member","role":"Investor & Entrepreneur","avatarUrl":"/img/people/voice-c-800.jpg"}]"""),
    };

    private static readonly Dictionary<string, Dictionary<string, BlockCopy>> Blocks = new()
    {
        ["hero"] = new()
        {
            ["de-DE"] = new("Wo Ehrgeiz auf Ausrichtung trifft.", "Eine private, globale Gemeinschaft für Online-Gründer, Investoren und Operatoren.", CtaLabel: "Zugang anfragen"),
            ["tr-TR"] = new("Hırsın hizaya geldiği yer.", "Online kurucular, yatırımcılar ve operatörler için özel ve küresel bir topluluk.", CtaLabel: "Erişim iste"),
            ["et-EE"] = new("Kus ambitsioon kohtub ühtsusega.", "Privaatne ülemaailmne kogukond veebiasutajatele, investoritele ja operaatoritele.", CtaLabel: "Taotle ligipääsu"),
        },
        ["feature-strip"] = new()
        {
            ["de-DE"] = new("Finden Sie, was für Sie zählt", ExtraJson: """[{"label":"Lernen","description":"Von Gründern und Operatoren, die aufgebaut haben, was Sie gerade aufbauen."},{"label":"Vernetzen","description":"Lernen Sie Menschen jenseits Ihres bisherigen Kreises kennen."},{"label":"Wachsen","description":"Zugang zu Frameworks und Strategien zum Skalieren."},{"label":"Erleben","description":"Treffen, aus denen bleibende Beziehungen entstehen."},{"label":"Dazugehören","description":"Das House geht über das Event hinaus weiter."}]"""),
            ["tr-TR"] = new("Sizin için önemli olanı bulun", ExtraJson: """[{"label":"Öğren","description":"Sizin kurduğunuzu daha önce kurmuş kuruculardan ve operatörlerden."},{"label":"Bağlan","description":"Mevcut çevrenizin ötesindeki insanlarla tanışın."},{"label":"Büyü","description":"Ölçeklenmek için çerçevelere ve stratejilere erişin."},{"label":"Deneyimle","description":"Kalıcı ilişkiler kuran buluşmalara katılın."},{"label":"Ait ol","description":"House, etkinlik bittikten sonra da devam eder."}]"""),
            ["et-EE"] = new("Leidke see, mis on teile oluline", ExtraJson: """[{"label":"Õpi","description":"Asutajatelt ja operaatoritelt, kes on ehitanud selle, mida teie praegu ehitate."},{"label":"Loo sidemeid","description":"Kohtuge inimestega väljaspool oma senist ringi."},{"label":"Kasva","description":"Ligipääs raamistikele ja strateegiatele kasvamiseks."},{"label":"Koge","description":"Kohtumised, millest sünnivad kestvad suhted."},{"label":"Kuulu","description":"House jätkub ka pärast sündmust."}]"""),
        },
        ["ecosystem"] = new()
        {
            ["de-DE"] = new("Mehr als Retreats. Ein vollständiges Netzwerk.", BodyText: "Erlebnisse sind der Anfang. Alles, was Sie zum Lernen, Vernetzen und Aufbauen brauchen, liegt an einem privaten Ort.", CtaLabel: "Alle Erlebnisse ansehen",
                ExtraJson: """[{"title":"Live- & On-Demand-Webinare","description":"Lernen Sie von führenden Gründern, Investoren und Experten aus Business, Finanzen, Marketing und persönlicher Entwicklung.","imageUrl":"/img/ecosystem/webinars-800.jpg","imageAlt":"Ein Redner auf der Bühne vor sitzendem Publikum","linkLabel":"Webinare ansehen","linkUrl":"/sessions"},{"title":"Community & Networking","description":"Treten Sie privaten Gruppen bei, treffen Sie Gleichgesinnte und arbeiten Sie an Projekten, die zählen.","imageUrl":"/img/ecosystem/community-800.jpg","imageAlt":"Eine Gruppe im Gespräch in einem hellen, offenen Arbeitsraum","linkLabel":"Zur Community","linkUrl":"/membership"},{"title":"Digitaler Marktplatz","description":"Entdecken und kaufen Sie hochwertige Business-Programme, Vorlagen, Ressourcen und Tools.","imageUrl":"/img/ecosystem/marketplace-800.jpg","imageAlt":"Ein Laptop und ein Notizbuch auf einem Schreibtisch am Fenster","linkLabel":"Zum Marktplatz","linkUrl":"/experiences"},{"title":"Signature-Retreats","description":"Transformierende Retreats an erstklassigen Orten, die Denken, Netzwerk und Geschäft auf ein neues Niveau heben.","imageUrl":"/img/ecosystem/retreats-800.jpg","imageAlt":"Eine Villenterrasse mit Pool bei Sonnenuntergang","linkLabel":"Retreats ansehen","linkUrl":"/experiences"}]"""),
            ["tr-TR"] = new("İnzivadan fazlası. Eksiksiz bir ağ.", BodyText: "Deneyimler yalnızca başlangıç. Öğrenmek, bağlantı kurmak ve inşa etmek için ihtiyacınız olan her şey tek bir özel yerde.", CtaLabel: "Tüm deneyimleri keşfedin",
                ExtraJson: """[{"title":"Canlı ve isteğe bağlı webinarlar","description":"İş, finans, pazarlama ve kişisel gelişim alanlarında önde gelen kuruculardan, yatırımcılardan ve uzmanlardan öğrenin.","imageUrl":"/img/ecosystem/webinars-800.jpg","imageAlt":"Oturan bir dinleyici kitlesinin önünde sahnedeki bir konuşmacı","linkLabel":"Webinarlara göz atın","linkUrl":"/sessions"},{"title":"Topluluk ve networking","description":"Özel gruplara katılın, benzer düşünen insanlarla tanışın ve önemli projelerde birlikte çalışın.","imageUrl":"/img/ecosystem/community-800.jpg","imageAlt":"Aydınlık, açık bir çalışma alanında sohbet eden bir grup","linkLabel":"Topluluğa girin","linkUrl":"/membership"},{"title":"Dijital pazar yeri","description":"Yüksek kaliteli iş programlarını, şablonları, kaynakları ve araçları keşfedin ve satın alın.","imageUrl":"/img/ecosystem/marketplace-800.jpg","imageAlt":"Pencere kenarındaki bir masada dizüstü bilgisayar ve defter","linkLabel":"Pazar yerine göz atın","linkUrl":"/experiences"},{"title":"İmza inzivalar","description":"Zihninizi, ağınızı ve işinizi bir üst seviyeye taşıyan, dünya çapında mekânlarda dönüştürücü inzivalara katılın.","imageUrl":"/img/ecosystem/retreats-800.jpg","imageAlt":"Gün batımında havuzlu bir villa terası","linkLabel":"İnzivaları görün","linkUrl":"/experiences"}]"""),
            ["et-EE"] = new("Rohkem kui retriidid. Terviklik võrgustik.", BodyText: "Elamused on alles algus. Kõik, mida vajate õppimiseks, suhtlemiseks ja ehitamiseks, on ühes privaatses kohas.", CtaLabel: "Vaata kõiki elamusi",
                ExtraJson: """[{"title":"Otse- ja järelvaadatavad veebiseminarid","description":"Õppige tippasutajatelt, investoritelt ja ekspertidelt äri, rahanduse, turunduse ja isikliku arengu vallas.","imageUrl":"/img/ecosystem/webinars-800.jpg","imageAlt":"Esineja laval istuva publiku ees","linkLabel":"Sirvi veebiseminare","linkUrl":"/sessions"},{"title":"Kogukond ja võrgustumine","description":"Liituge privaatsete gruppidega, kohtuge mõttekaaslastega ja tehke koostööd olulistes projektides.","imageUrl":"/img/ecosystem/community-800.jpg","imageAlt":"Grupp vestlemas valges avatud tööruumis","linkLabel":"Sisene kogukonda","linkUrl":"/membership"},{"title":"Digitaalne turg","description":"Avastage ja ostke kvaliteetseid äriprogramme, malle, ressursse ja tööriistu.","imageUrl":"/img/ecosystem/marketplace-800.jpg","imageAlt":"Sülearvuti ja märkmik laual akna all","linkLabel":"Sirvi turgu","linkUrl":"/experiences"},{"title":"Eriretriidid","description":"Liituge muutvate retriitidega maailmatasemel paikades, mis tõstavad teie mõtlemist, võrgustikku ja äri.","imageUrl":"/img/ecosystem/retreats-800.jpg","imageAlt":"Villa terass ja bassein päikeseloojangul","linkLabel":"Vaata retriite","linkUrl":"/experiences"}]"""),
        },
        ["stats"] = new()
        {
            ["de-DE"] = new(ExtraJson: """[{"value":"180+","label":"Mitglieder"},{"value":"24","label":"Länder"},{"value":"65+","label":"Erlebnisse"},{"value":"40+","label":"Experten"}]"""),
            ["tr-TR"] = new(ExtraJson: """[{"value":"180+","label":"Üye"},{"value":"24","label":"Ülke"},{"value":"65+","label":"Deneyim"},{"value":"40+","label":"Uzman"}]"""),
            ["et-EE"] = new(ExtraJson: """[{"value":"180+","label":"Liiget"},{"value":"24","label":"Riiki"},{"value":"65+","label":"Elamust"},{"value":"40+","label":"Eksperti"}]"""),
        },
        ["trust"] = new()
        {
            ["de-DE"] = new("Ernsthafte Macher finden hier ihre Menschen.", "Eine Gemeinschaft, die Vermächtnisse schafft", "Im VI House finden ernsthafte Macher ihre Menschen und gestalten gemeinsam die Zukunft.", "Mitglied werden",
                """[{"quote":"Die Kontakte aus dem VI House haben mein Geschäft und mein Leben verändert.","author":"Placeholder Member","role":"E-Commerce-Gründer","avatarUrl":"/img/people/voice-a-800.jpg"},{"quote":"Die beste Gemeinschaft erstklassiger Operatoren, der ich je angehört habe.","author":"Placeholder Member","role":"Digital Creator","avatarUrl":"/img/people/voice-b-800.jpg"},{"quote":"Die Retreats sind unvergleichlich. Echte Transformation.","author":"Placeholder Member","role":"Investor & Unternehmer","avatarUrl":"/img/people/voice-c-800.jpg"}]"""),
            ["tr-TR"] = new("Ciddi kurucular kendi insanlarını burada bulur.", "Kalıcı bir miras kuran topluluk", "The VI House, ciddi kurucuların kendi insanlarını bulduğu ve geleceği birlikte inşa ettiği yerdir.", "Üye olun",
                """[{"quote":"VI House'ta kurduğum bağlantılar işimi ve hayatımı değiştirdi.","author":"Placeholder Member","role":"E-ticaret kurucusu","avatarUrl":"/img/people/voice-a-800.jpg"},{"quote":"Parçası olduğum en iyi üst düzey operatör topluluğu.","author":"Placeholder Member","role":"Dijital içerik üreticisi","avatarUrl":"/img/people/voice-b-800.jpg"},{"quote":"İnzivaların bir eşi yok. Tam anlamıyla dönüşüm.","author":"Placeholder Member","role":"Yatırımcı ve girişimci","avatarUrl":"/img/people/voice-c-800.jpg"}]"""),
            ["et-EE"] = new("Tõsised ehitajad leiavad siit oma inimesed.", "Kogukond, mis loob pärandit", "The VI House on koht, kus tõsised ehitajad leiavad oma inimesed ja loovad koos tulevikku.", "Hakka liikmeks",
                """[{"quote":"VI House'is loodud sidemed muutsid mu äri ja elu.","author":"Placeholder Member","role":"E-kaubanduse asutaja","avatarUrl":"/img/people/voice-a-800.jpg"},{"quote":"Parim kõrgetasemeliste operaatorite kogukond, mille liige ma kunagi olnud olen.","author":"Placeholder Member","role":"Digisisu looja","avatarUrl":"/img/people/voice-b-800.jpg"},{"quote":"Retriidid on võrratud. Puhas muutus.","author":"Placeholder Member","role":"Investor ja ettevõtja","avatarUrl":"/img/people/voice-c-800.jpg"}]"""),
        },
    };

    private record PostCopy(string Title, string Excerpt, string Body);

    /// <summary>Seeded journal posts: slug → English title (the match) and the other three languages.</summary>
    private static readonly Dictionary<string, (string EnglishTitle, Dictionary<string, PostCopy> Copy)> Posts = new()
    {
        ["the-quiet-signal-reading-capital-before-it-moves"] = ("The Quiet Signal: Reading Capital Before It Moves", new()
        {
            ["de-DE"] = new("Das leise Signal: Kapital lesen, bevor es sich bewegt",
                "Die Gründer, die gut Kapital aufnehmen, sind selten die, die am lautesten pitchen.",
                "<p>Kapital kündigt sich selten an, bevor es sich bewegt. Wenn eine Finanzierungsrunde öffentlich wird, besteht die Beziehung, die sie möglich gemacht hat, meist schon seit Monaten.</p><p>Das spricht für Räume statt Kaltakquise — die Gründer, die gut Kapital aufnehmen, waren meist schon persönlich bekannt, bevor sie etwas brauchten.</p>"),
            ["tr-TR"] = new("Sessiz Sinyal: Sermayeyi Harekete Geçmeden Okumak",
                "İyi yatırım alan kurucular nadiren en yüksek sesle sunum yapanlardır.",
                "<p>Sermaye, harekete geçmeden önce kendini nadiren belli eder. Bir yatırım turu duyurulduğunda, onu mümkün kılan ilişki çoğu zaman aylardır vardır.</p><p>Soğuk iletişim yerine doğru odaların önemi de buradan gelir — iyi yatırım alan kurucular genellikle bir şeye ihtiyaç duymadan önce yüz yüze tanınan kişilerdir.</p>"),
            ["et-EE"] = new("Vaikne signaal: kapitali lugemine enne, kui see liigub",
                "Asutajad, kes kaasavad hästi raha, on harva need, kes pitchivad kõige valjemini.",
                "<p>Kapital annab endast harva teada enne, kui see liigub. Selleks ajaks, kui rahastusring avalikuks saab, on seda võimaldanud suhe tavaliselt kestnud juba kuid.</p><p>Just seepärast on olulised õiged ruumid, mitte külmad pöördumised — hästi raha kaasavad asutajad on tavaliselt need, keda tunti isiklikult juba enne, kui neil midagi vaja oli.</p>"),
        }),
        ["inside-the-room-what-makes-a-founder-session-work"] = ("Inside the Room: What Makes a Founder Session Work", new()
        {
            ["de-DE"] = new("Im Raum: Was eine Gründer-Session gelingen lässt",
                "Notizen aus dem House dazu, wie eine Session entsteht, über die man noch ein Jahr später spricht.",
                "<p>Eine gute Gründer-Session hat fast nichts mit der Agenda zu tun.</p><p>Sie hat alles damit zu tun, wer im Raum ist, wie klein er bleibt und ob die Menschen sich trauen, das zu sagen, weswegen sie eigentlich gekommen sind.</p>"),
            ["tr-TR"] = new("Odanın İçinden: Bir Kurucu Oturumunu Başarılı Kılan Nedir",
                "Bir yıl sonra bile konuşulan bir oturumu nasıl yürüttüğümüze dair House'tan notlar.",
                "<p>İyi bir kurucu oturumunun gündemle neredeyse hiçbir ilgisi yoktur.</p><p>Her şey odada kimin olduğuyla, odanın ne kadar küçük kaldığıyla ve insanların asıl söylemeye geldikleri şeyi söyleyebildiklerini hissedip hissetmedikleriyle ilgilidir.</p>"),
            ["et-EE"] = new("Ruumis sees: mis teeb asutajate sessioonist õnnestunud sessiooni",
                "House'i märkmed sellest, kuidas pidada sessiooni, millest räägitakse veel aasta pärast.",
                "<p>Heal asutajate sessioonil pole päevakorraga peaaegu mingit pistmist.</p><p>Kõik sõltub sellest, kes ruumis on, kui väikeseks see jääb ja kas inimesed tunnevad, et saavad öelda seda, mida nad tegelikult ütlema tulid.</p>"),
        }),
        ["building-in-public-without-burning-out"] = ("Building in Public Without Burning Out", new()
        {
            ["de-DE"] = new("Öffentlich aufbauen, ohne auszubrennen", "Entwurf — wird noch geschrieben.", "<p>Entwurf, wird vom Team noch geschrieben.</p>"),
            ["tr-TR"] = new("Tükenmeden Herkesin Önünde İnşa Etmek", "Taslak — hâlâ yazılıyor.", "<p>Taslak; ekip tarafından hâlâ yazılıyor.</p>"),
            ["et-EE"] = new("Avalikult ehitamine ilma läbi põlemata", "Mustand — alles kirjutamisel.", "<p>Mustand, meeskond kirjutab seda alles.</p>"),
        }),
    };

    public static async Task ApplyAsync(VIHouseDbContext db, CancellationToken ct = default)
    {
        var home = await db.ContentPages.Include(p => p.Blocks).ThenInclude(b => b.Translations)
            .FirstOrDefaultAsync(p => p.Slug == "home", ct);

        foreach (var block in home?.Blocks ?? [])
        {
            if (!Blocks.TryGetValue(block.SectionKey, out var copies) || !EnglishBlocks.TryGetValue(block.SectionKey, out var english)) continue;
            if (block.Heading != english.Heading || !SameJson(block.ExtraJson, english.ExtraJson)) continue;

            foreach (var (culture, copy) in copies)
            {
                if (block.Translations.Any(t => t.Culture == culture)) continue;
                db.ContentBlockTranslations.Add(new ContentBlockTranslation
                {
                    ContentBlockId = block.Id,
                    Culture = culture,
                    Heading = copy.Heading,
                    Subheading = copy.Subheading,
                    BodyText = copy.BodyText,
                    CtaLabel = copy.CtaLabel,
                    ExtraJson = copy.ExtraJson,
                });
            }
        }

        var slugs = Posts.Keys.ToList();
        var posts = await db.JournalPosts.Include(p => p.Translations).Where(p => slugs.Contains(p.Slug)).ToListAsync(ct);
        foreach (var post in posts)
        {
            var (englishTitle, copies) = Posts[post.Slug];
            if (post.Translations.FirstOrDefault(t => t.Culture == "en-GB")?.Title != englishTitle) continue;

            foreach (var (culture, copy) in copies)
            {
                if (post.Translations.Any(t => t.Culture == culture)) continue;
                db.JournalPostTranslations.Add(new JournalPostTranslation
                {
                    JournalPostId = post.Id,
                    Culture = culture,
                    Title = copy.Title,
                    Excerpt = copy.Excerpt,
                    Body = copy.Body,
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static bool SameJson(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b);
        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(a), JsonNode.Parse(b));
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
