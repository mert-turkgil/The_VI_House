using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIHouse.DataAccess.Concrete.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class BackfillHomeBlockTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data only. The homepage's German, Turkish and Estonian copy lives in
            // ContentBlockTranslations and is edited under Admin > Content — it was never part of
            // the seed, so a database that predates it (production) shows English on every
            // language. Each row is inserted only where the block exists and no translation for
            // that culture does, so an admin's own edits are never overwritten and re-running is
            // harmless.
            migrationBuilder.Sql(@"
INSERT INTO ContentBlockTranslations (Id, ContentBlockId, Culture, Heading, Subheading, BodyText, CtaLabel, ExtraJson, CreatedAt)
SELECT NEWID(), b.Id, N'de-DE', N'Mehr als Retreats. Ein vollständiges Ökosystem für Wachstum.', N'Das VI House Ökosystem', N'Retreats sind erst der Anfang. Alles zum Lernen, Vernetzen, Zusammenarbeiten und Skalieren — an einem einzigen privaten Ort.', N'Alle Funktionen entdecken', N'[
  {
    ""title"": ""Live- und On-Demand-Webinare"",
    ""description"": ""Lernen Sie von führenden Gründern, Investoren und Fachleuten aus Wirtschaft, Finanzen, Marketing und persönlicher Entwicklung."",
    ""imageUrl"": ""/img/ecosystem/webinars-800.jpg"",
    ""imageAlt"": ""Ein Redner auf der Bühne vor sitzendem Publikum"",
    ""linkLabel"": ""Webinare ansehen"",
    ""linkUrl"": ""/sessions""
  },
  {
    ""title"": ""Community & Netzwerk"",
    ""description"": ""Treten Sie privaten Gruppen bei, treffen Sie Gleichgesinnte und arbeiten Sie an den Projekten, die zählen."",
    ""imageUrl"": ""/img/ecosystem/community-800.jpg"",
    ""imageAlt"": ""Eine Gruppe im Gespräch in einem hellen, offenen Arbeitsraum"",
    ""linkLabel"": ""Zur Community"",
    ""linkUrl"": ""/membership""
  },
  {
    ""title"": ""Digitaler Marktplatz"",
    ""description"": ""Entdecken und erwerben Sie hochwertige Business-Programme, Vorlagen, Ressourcen und Werkzeuge."",
    ""imageUrl"": ""/img/ecosystem/marketplace-800.jpg"",
    ""imageAlt"": ""Ein Laptop und ein Notizbuch auf einem Schreibtisch am Fenster"",
    ""linkLabel"": ""Marktplatz ansehen"",
    ""linkUrl"": ""/experiences""
  },
  {
    ""title"": ""Signature-Retreats"",
    ""description"": ""Nehmen Sie an Retreats an erstklassigen Orten teil, die Denken, Netzwerk und Geschäft weiterbringen."",
    ""imageUrl"": ""/img/ecosystem/retreats-800.jpg"",
    ""imageAlt"": ""Eine Villenterrasse mit Pool bei Sonnenuntergang"",
    ""linkLabel"": ""Retreats ansehen"",
    ""linkUrl"": ""/experiences""
  }
]', SYSDATETIMEOFFSET()
FROM ContentBlocks b JOIN ContentPages p ON p.Id = b.PageId
WHERE p.Slug = 'home' AND b.SectionKey = N'ecosystem'
  AND NOT EXISTS (SELECT 1 FROM ContentBlockTranslations t WHERE t.ContentBlockId = b.Id AND t.Culture = N'de-DE');

INSERT INTO ContentBlockTranslations (Id, ContentBlockId, Culture, Heading, Subheading, BodyText, CtaLabel, ExtraJson, CreatedAt)
SELECT NEWID(), b.Id, N'et-EE', N'Leidke see, mis teile korda läheb', NULL, NULL, NULL, N'[
  {""icon"":""learn"",""label"":""Õppige"",""description"":""Asutajatelt ja juhtidelt, kes on juba ehitanud selle, mida teie praegu ehitate.""},
  {""icon"":""connect"",""label"":""Looge sidemeid"",""description"":""Kohtuge inimestega väljaspool oma senist ringi.""},
  {""icon"":""grow"",""label"":""Kasvage"",""description"":""Ligipääs raamistikele ja strateegiatele, mis aitavad kasvada.""},
  {""icon"":""experience"",""label"":""Kogege"",""description"":""Osalege kohtumistel, kust sünnivad püsivad suhted.""},
  {""icon"":""belong"",""label"":""Kuuluge"",""description"":""House ei lõpe ürituse lõppedes.""}
]', SYSDATETIMEOFFSET()
FROM ContentBlocks b JOIN ContentPages p ON p.Id = b.PageId
WHERE p.Slug = 'home' AND b.SectionKey = N'feature-strip'
  AND NOT EXISTS (SELECT 1 FROM ContentBlockTranslations t WHERE t.ContentBlockId = b.Id AND t.Culture = N'et-EE');

INSERT INTO ContentBlockTranslations (Id, ContentBlockId, Culture, Heading, Subheading, BodyText, CtaLabel, ExtraJson, CreatedAt)
SELECT NEWID(), b.Id, N'tr-TR', NULL, NULL, NULL, NULL, N'[
  {
    ""value"": ""180+"",
    ""label"": ""Üye""
  },
  {
    ""value"": ""24"",
    ""label"": ""Ülke""
  },
  {
    ""value"": ""65+"",
    ""label"": ""Deneyim""
  },
  {
    ""value"": ""40+"",
    ""label"": ""Uzman""
  }
]', SYSDATETIMEOFFSET()
FROM ContentBlocks b JOIN ContentPages p ON p.Id = b.PageId
WHERE p.Slug = 'home' AND b.SectionKey = N'stats'
  AND NOT EXISTS (SELECT 1 FROM ContentBlockTranslations t WHERE t.ContentBlockId = b.Id AND t.Culture = N'tr-TR');

INSERT INTO ContentBlockTranslations (Id, ContentBlockId, Culture, Heading, Subheading, BodyText, CtaLabel, ExtraJson, CreatedAt)
SELECT NEWID(), b.Id, N'de-DE', N'Finden Sie, worauf es Ihnen ankommt', NULL, NULL, NULL, N'[
  {""icon"":""learn"",""label"":""Lernen"",""description"":""Von Gründern und Operators, die bereits gebaut haben, was Sie gerade bauen.""},
  {""icon"":""connect"",""label"":""Vernetzen"",""description"":""Lernen Sie Menschen außerhalb Ihres bisherigen Kreises kennen.""},
  {""icon"":""grow"",""label"":""Wachsen"",""description"":""Zugang zu Frameworks und Strategien fürs Skalieren.""},
  {""icon"":""experience"",""label"":""Erleben"",""description"":""Treffen Sie sich dort, wo dauerhafte Beziehungen entstehen.""},
  {""icon"":""belong"",""label"":""Dazugehören"",""description"":""Das House hört nach der Veranstaltung nicht auf.""}
]', SYSDATETIMEOFFSET()
FROM ContentBlocks b JOIN ContentPages p ON p.Id = b.PageId
WHERE p.Slug = 'home' AND b.SectionKey = N'feature-strip'
  AND NOT EXISTS (SELECT 1 FROM ContentBlockTranslations t WHERE t.ContentBlockId = b.Id AND t.Culture = N'de-DE');

INSERT INTO ContentBlockTranslations (Id, ContentBlockId, Culture, Heading, Subheading, BodyText, CtaLabel, ExtraJson, CreatedAt)
SELECT NEWID(), b.Id, N'et-EE', NULL, NULL, NULL, NULL, N'[
  {
    ""value"": ""180+"",
    ""label"": ""Liiget""
  },
  {
    ""value"": ""24"",
    ""label"": ""Riiki""
  },
  {
    ""value"": ""65+"",
    ""label"": ""Elamust""
  },
  {
    ""value"": ""40+"",
    ""label"": ""Eksperti""
  }
]', SYSDATETIMEOFFSET()
FROM ContentBlocks b JOIN ContentPages p ON p.Id = b.PageId
WHERE p.Slug = 'home' AND b.SectionKey = N'stats'
  AND NOT EXISTS (SELECT 1 FROM ContentBlockTranslations t WHERE t.ContentBlockId = b.Id AND t.Culture = N'et-EE');

INSERT INTO ContentBlockTranslations (Id, ContentBlockId, Culture, Heading, Subheading, BodyText, CtaLabel, ExtraJson, CreatedAt)
SELECT NEWID(), b.Id, N'et-EE', N'Enam kui retriidid. Terviklik kasvu ökosüsteem.', N'The VI House''i ökosüsteem', N'Retriidid on alles algus. Kõik, mida on vaja õppimiseks, sidemete loomiseks, koostööks ja kasvuks — ühes privaatses kohas.', N'Vaadake kõiki võimalusi', N'[
  {
    ""title"": ""Otse- ja salvestatud veebiseminarid"",
    ""description"": ""Õppige tippasutajatelt, investoritelt ja ekspertidelt ärist, rahandusest, turundusest ja isiklikust arengust."",
    ""imageUrl"": ""/img/ecosystem/webinars-800.jpg"",
    ""imageAlt"": ""Esineja laval istuva publiku ees"",
    ""linkLabel"": ""Vaadake veebiseminare"",
    ""linkUrl"": ""/sessions""
  },
  {
    ""title"": ""Kogukond ja võrgustik"",
    ""description"": ""Liituge privaatsete gruppidega, kohtuge sarnaste inimestega ja tehke koostööd projektides, mis loevad."",
    ""imageUrl"": ""/img/ecosystem/community-800.jpg"",
    ""imageAlt"": ""Vestlev seltskond avaras valges tööruumis"",
    ""linkLabel"": ""Sisenege kogukonda"",
    ""linkUrl"": ""/membership""
  },
  {
    ""title"": ""Digitaalne turuplats"",
    ""description"": ""Avastage ja soetage kvaliteetseid äriprogramme, malle, materjale ja tööriistu."",
    ""imageUrl"": ""/img/ecosystem/marketplace-800.jpg"",
    ""imageAlt"": ""Sülearvuti ja märkmik akna all laual"",
    ""linkLabel"": ""Vaadake turuplatsi"",
    ""linkUrl"": ""/experiences""
  },
  {
    ""title"": ""Signatuurretriidid"",
    ""description"": ""Osalege maailmatasemel paikades toimuvatel retriitidel, mis viivad edasi teie mõtte, võrgustiku ja äri."",
    ""imageUrl"": ""/img/ecosystem/retreats-800.jpg"",
    ""imageAlt"": ""Villa terrass ja bassein päikeseloojangul"",
    ""linkLabel"": ""Vaadake retriite"",
    ""linkUrl"": ""/experiences""
  }
]', SYSDATETIMEOFFSET()
FROM ContentBlocks b JOIN ContentPages p ON p.Id = b.PageId
WHERE p.Slug = 'home' AND b.SectionKey = N'ecosystem'
  AND NOT EXISTS (SELECT 1 FROM ContentBlockTranslations t WHERE t.ContentBlockId = b.Id AND t.Culture = N'et-EE');

INSERT INTO ContentBlockTranslations (Id, ContentBlockId, Culture, Heading, Subheading, BodyText, CtaLabel, ExtraJson, CreatedAt)
SELECT NEWID(), b.Id, N'tr-TR', N'İnzivalardan fazlası. Eksiksiz bir büyüme ekosistemi.', N'The VI House Ekosistemi', N'İnzivalar yalnızca başlangıç. Öğrenmek, bağlantı kurmak, birlikte çalışmak ve ölçeklenmek için gereken her şey tek bir özel yerde.', N'Tüm özellikleri keşfedin', N'[
  {
    ""title"": ""Canlı ve İstediğiniz Zaman Web Seminerleri"",
    ""description"": ""İş, finans, pazarlama ve kişisel gelişim alanlarındaki önde gelen kurucu, yatırımcı ve uzmanlardan öğrenin."",
    ""imageUrl"": ""/img/ecosystem/webinars-800.jpg"",
    ""imageAlt"": ""Oturan bir dinleyici kitlesinin önünde sahnedeki konuşmacı"",
    ""linkLabel"": ""Web seminerlerine göz atın"",
    ""linkUrl"": ""/sessions""
  },
  {
    ""title"": ""Topluluk ve Bağlantılar"",
    ""description"": ""Özel gruplara katılın, sizinle aynı yolda olan insanlarla tanışın ve önemli projelerde birlikte çalışın."",
    ""imageUrl"": ""/img/ecosystem/community-800.jpg"",
    ""imageAlt"": ""Aydınlık ve açık bir çalışma alanında sohbet eden bir grup"",
    ""linkLabel"": ""Topluluğa girin"",
    ""linkUrl"": ""/membership""
  },
  {
    ""title"": ""Dijital Pazar Yeri"",
    ""description"": ""Yüksek kaliteli iş programlarını, şablonları, kaynakları ve araçları keşfedin ve edinin."",
    ""imageUrl"": ""/img/ecosystem/marketplace-800.jpg"",
    ""imageAlt"": ""Pencere kenarındaki bir masada dizüstü bilgisayar ve defter"",
    ""linkLabel"": ""Pazar yerine göz atın"",
    ""linkUrl"": ""/experiences""
  },
  {
    ""title"": ""İmza İnzivalar"",
    ""description"": ""Zihninizi, çevrenizi ve işinizi ileri taşıyan, dünya standartlarında mekânlardaki dönüştürücü inzivalara katılın."",
    ""imageUrl"": ""/img/ecosystem/retreats-800.jpg"",
    ""imageAlt"": ""Gün batımında villa terası ve havuz"",
    ""linkLabel"": ""İnzivaları görün"",
    ""linkUrl"": ""/experiences""
  }
]', SYSDATETIMEOFFSET()
FROM ContentBlocks b JOIN ContentPages p ON p.Id = b.PageId
WHERE p.Slug = 'home' AND b.SectionKey = N'ecosystem'
  AND NOT EXISTS (SELECT 1 FROM ContentBlockTranslations t WHERE t.ContentBlockId = b.Id AND t.Culture = N'tr-TR');

INSERT INTO ContentBlockTranslations (Id, ContentBlockId, Culture, Heading, Subheading, BodyText, CtaLabel, ExtraJson, CreatedAt)
SELECT NEWID(), b.Id, N'de-DE', N'Hier finden ernsthafte Macher ihre Leute.', N'Eine Gemeinschaft, die Bleibendes schafft', N'The VI House ist der Ort, an dem ernsthafte Macher ihre Leute finden und gemeinsam die Zukunft bauen.', N'Mitglied werden', N'[
  {
    ""quote"": ""Die Kontakte, die ich im VI House geknüpft habe, haben mein Geschäft und mein Leben verändert."",
    ""author"": ""Platzhalter-Mitglied"",
    ""role"": ""E-Commerce-Gründerin"",
    ""avatarUrl"": ""/img/people/voice-a-800.jpg""
  },
  {
    ""quote"": ""Die beste Gemeinschaft von Operators auf hohem Niveau, in der ich je war."",
    ""author"": ""Platzhalter-Mitglied"",
    ""role"": ""Digital Creator"",
    ""avatarUrl"": ""/img/people/voice-b-800.jpg""
  },
  {
    ""quote"": ""Die Retreats sind unerreicht. Reine Veränderung."",
    ""author"": ""Platzhalter-Mitglied"",
    ""role"": ""Investor und Unternehmer"",
    ""avatarUrl"": ""/img/people/voice-c-800.jpg""
  }
]', SYSDATETIMEOFFSET()
FROM ContentBlocks b JOIN ContentPages p ON p.Id = b.PageId
WHERE p.Slug = 'home' AND b.SectionKey = N'trust'
  AND NOT EXISTS (SELECT 1 FROM ContentBlockTranslations t WHERE t.ContentBlockId = b.Id AND t.Culture = N'de-DE');

INSERT INTO ContentBlockTranslations (Id, ContentBlockId, Culture, Heading, Subheading, BodyText, CtaLabel, ExtraJson, CreatedAt)
SELECT NEWID(), b.Id, N'de-DE', N'Wo Ambition auf Ausrichtung trifft.', N'Eine private globale Gemeinschaft für Online-Gründer, Investoren und Operators.', NULL, N'Zugang anfragen', NULL, SYSDATETIMEOFFSET()
FROM ContentBlocks b JOIN ContentPages p ON p.Id = b.PageId
WHERE p.Slug = 'home' AND b.SectionKey = N'hero'
  AND NOT EXISTS (SELECT 1 FROM ContentBlockTranslations t WHERE t.ContentBlockId = b.Id AND t.Culture = N'de-DE');

INSERT INTO ContentBlockTranslations (Id, ContentBlockId, Culture, Heading, Subheading, BodyText, CtaLabel, ExtraJson, CreatedAt)
SELECT NEWID(), b.Id, N'tr-TR', N'Hırsın ve uyumun buluştuğu yer.', N'Çevrimiçi kurucular, yatırımcılar ve operatörler için özel bir küresel topluluk.', NULL, N'Erişim talep edin', NULL, SYSDATETIMEOFFSET()
FROM ContentBlocks b JOIN ContentPages p ON p.Id = b.PageId
WHERE p.Slug = 'home' AND b.SectionKey = N'hero'
  AND NOT EXISTS (SELECT 1 FROM ContentBlockTranslations t WHERE t.ContentBlockId = b.Id AND t.Culture = N'tr-TR');

INSERT INTO ContentBlockTranslations (Id, ContentBlockId, Culture, Heading, Subheading, BodyText, CtaLabel, ExtraJson, CreatedAt)
SELECT NEWID(), b.Id, N'tr-TR', N'Sizin için önemli olanı bulun', NULL, NULL, NULL, N'[
  {
    ""icon"": ""learn"",
    ""label"": ""Öğrenin"",
    ""description"": ""Sizin inşa ettiğinizi daha önce inşa etmiş kurucu ve operatörlerden.""
  },
  {
    ""icon"": ""connect"",
    ""label"": ""Bağlanın"",
    ""description"": ""Mevcut çevrenizin ötesindeki insanlarla tanışın.""
  },
  {
    ""icon"": ""grow"",
    ""label"": ""Büyüyün"",
    ""description"": ""Ölçeklenmek için gereken yöntem ve stratejilere erişin.""
  },
  {
    ""icon"": ""experience"",
    ""label"": ""Deneyimleyin"",
    ""description"": ""Kalıcı ilişkiler kuran buluşmalara katılın.""
  },
  {
    ""icon"": ""belong"",
    ""label"": ""Ait olun"",
    ""description"": ""House, etkinlik bittiğinde sona ermez.""
  }
]', SYSDATETIMEOFFSET()
FROM ContentBlocks b JOIN ContentPages p ON p.Id = b.PageId
WHERE p.Slug = 'home' AND b.SectionKey = N'feature-strip'
  AND NOT EXISTS (SELECT 1 FROM ContentBlockTranslations t WHERE t.ContentBlockId = b.Id AND t.Culture = N'tr-TR');

INSERT INTO ContentBlockTranslations (Id, ContentBlockId, Culture, Heading, Subheading, BodyText, CtaLabel, ExtraJson, CreatedAt)
SELECT NEWID(), b.Id, N'et-EE', N'Tõsised tegijad leiavad siit oma inimesed.', N'Kogukond, mis loob püsivat', N'The VI House on koht, kus tõsised tegijad leiavad oma inimesed ja loovad koos tulevikku.', N'Astuge liikmeks', N'[
  {
    ""quote"": ""VI House''is loodud sidemed muutsid nii mu äri kui ka elu."",
    ""author"": ""Näidisliige"",
    ""role"": ""E-kaubanduse asutaja"",
    ""avatarUrl"": ""/img/people/voice-a-800.jpg""
  },
  {
    ""quote"": ""Parim kõrgetasemeliste tegijate kogukond, kuhu ma kunagi kuulunud olen."",
    ""author"": ""Näidisliige"",
    ""role"": ""Digitaalne looja"",
    ""avatarUrl"": ""/img/people/voice-b-800.jpg""
  },
  {
    ""quote"": ""Retriidid on võrreldamatud. Puhas muutus."",
    ""author"": ""Näidisliige"",
    ""role"": ""Investor ja ettevõtja"",
    ""avatarUrl"": ""/img/people/voice-c-800.jpg""
  }
]', SYSDATETIMEOFFSET()
FROM ContentBlocks b JOIN ContentPages p ON p.Id = b.PageId
WHERE p.Slug = 'home' AND b.SectionKey = N'trust'
  AND NOT EXISTS (SELECT 1 FROM ContentBlockTranslations t WHERE t.ContentBlockId = b.Id AND t.Culture = N'et-EE');

INSERT INTO ContentBlockTranslations (Id, ContentBlockId, Culture, Heading, Subheading, BodyText, CtaLabel, ExtraJson, CreatedAt)
SELECT NEWID(), b.Id, N'et-EE', N'Kus ambitsioon kohtub selgusega.', N'Privaatne ülemaailmne kogukond veebiasutajatele, investoritele ja juhtidele.', NULL, N'Taotlege ligipääsu', NULL, SYSDATETIMEOFFSET()
FROM ContentBlocks b JOIN ContentPages p ON p.Id = b.PageId
WHERE p.Slug = 'home' AND b.SectionKey = N'hero'
  AND NOT EXISTS (SELECT 1 FROM ContentBlockTranslations t WHERE t.ContentBlockId = b.Id AND t.Culture = N'et-EE');

INSERT INTO ContentBlockTranslations (Id, ContentBlockId, Culture, Heading, Subheading, BodyText, CtaLabel, ExtraJson, CreatedAt)
SELECT NEWID(), b.Id, N'de-DE', NULL, NULL, NULL, NULL, N'[
  {
    ""value"": ""180+"",
    ""label"": ""Mitglieder""
  },
  {
    ""value"": ""24"",
    ""label"": ""Länder""
  },
  {
    ""value"": ""65+"",
    ""label"": ""Experiences""
  },
  {
    ""value"": ""40+"",
    ""label"": ""Expertinnen und Experten""
  }
]', SYSDATETIMEOFFSET()
FROM ContentBlocks b JOIN ContentPages p ON p.Id = b.PageId
WHERE p.Slug = 'home' AND b.SectionKey = N'stats'
  AND NOT EXISTS (SELECT 1 FROM ContentBlockTranslations t WHERE t.ContentBlockId = b.Id AND t.Culture = N'de-DE');

INSERT INTO ContentBlockTranslations (Id, ContentBlockId, Culture, Heading, Subheading, BodyText, CtaLabel, ExtraJson, CreatedAt)
SELECT NEWID(), b.Id, N'tr-TR', N'Ciddi kurucular burada kendi insanlarını buluyor.', N'Kalıcı bir miras kuran topluluk', N'The VI House, ciddi kurucuların kendi insanlarını bulduğu ve geleceği birlikte inşa ettiği yerdir.', N'Üye olun', N'[
  {
    ""quote"": ""VI House''ta kurduğum bağlantılar hem işimi hem hayatımı değiştirdi."",
    ""author"": ""Örnek Üye"",
    ""role"": ""E-ticaret kurucusu"",
    ""avatarUrl"": ""/img/people/voice-a-800.jpg""
  },
  {
    ""quote"": ""Bugüne kadar parçası olduğum en iyi üst düzey operatör topluluğu."",
    ""author"": ""Örnek Üye"",
    ""role"": ""Dijital içerik üreticisi"",
    ""avatarUrl"": ""/img/people/voice-b-800.jpg""
  },
  {
    ""quote"": ""İnzivaların eşi benzeri yok. Tam anlamıyla bir dönüşüm."",
    ""author"": ""Örnek Üye"",
    ""role"": ""Yatırımcı ve girişimci"",
    ""avatarUrl"": ""/img/people/voice-c-800.jpg""
  }
]', SYSDATETIMEOFFSET()
FROM ContentBlocks b JOIN ContentPages p ON p.Id = b.PageId
WHERE p.Slug = 'home' AND b.SectionKey = N'trust'
  AND NOT EXISTS (SELECT 1 FROM ContentBlockTranslations t WHERE t.ContentBlockId = b.Id AND t.Culture = N'tr-TR');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
