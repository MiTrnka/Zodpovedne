DELETE FROM public."Categories";
ALTER SEQUENCE "Categories_Id_seq" RESTART WITH 1;

INSERT INTO "Categories" ("DisplayOrder", "Name", "Code", "Description", "CreatedAt", "ImagePath")
VALUES
(10, 'Události a srazy', 'udalosti-srazy', 'Pozvánky na akce, výlety, turnaje, setkání...', NOW(), 'category-img.svg'),
(20, 'Cestování', 'cestovani', 'Tipy, zážitky a diskuze o cestování', NOW(), 'category-img.svg'),
(30, 'Hudba', 'hudba', 'Kapely, koncerty a vše o hudbě', NOW(), 'category-img.svg'),
(40, 'Filmy a seriály', 'filmy-serialy', 'Recenze, doporučení a diskuze o filmech', NOW(), 'category-img.svg'),
(50, 'Literatura', 'literatura', 'Knihy, povídky a literární tvorba', NOW(), 'category-img.svg'),
(60, 'Hry', 'hry', 'Počítačové, mobilní a deskové hry', NOW(), 'category-img.svg'),
(70, 'Zábava a volný čas', 'zabava-volny-cas', 'Co dělat ve volném čase', NOW(), 'category-img.svg'),
(80, 'Technologie a věda', 'technologie-veda', 'Novinky a diskuze o technologiích a vědě', NOW(), 'category-img.svg'),
(90, 'Studium a vzdělávání', 'studium-vzdelavani', 'Škola, studium a sebevzdělávání', NOW(), 'category-img.svg'),
(100, 'Kariéra a práce', 'kariera-prace', 'Práce, podnikání a profesní růst', NOW(), 'category-img.svg'),
(110, 'Zvířata', 'zvirata', 'Domácí mazlíčci a zvířecí svět', NOW(), 'category-img.svg'),
(120, 'Sport', 'sport', 'Sportovní dění, kluby a události', NOW(), 'category-img.svg'),
(130, 'Vaření a recepty', 'vareni-recepty', 'Recepty, vaření a kuchařské tipy', NOW(), 'category-img.svg'),
(140, 'Zdraví a životní styl', 'zdravi-zivotni-styl', 'Zdraví, pohyb a osobní rozvoj', NOW(), 'category-img.svg'),
(150, 'Domácnost a bydlení', 'domacnost-bydleni', 'Bydlení, úklid, vychytávky', NOW(), 'category-img.svg'),
(160, 'Společnost a politika', 'spolecnost-politika', 'Aktuální dění a názory na společnost', NOW(), 'category-img.svg'),
(170, 'Skutečné příběhy', 'skutecne-pribehy', 'Sdílení skutečných příběhů a zážitků', NOW(), 'category-img.svg'),
(180, 'Ostatní', 'ostatni', 'Na co zde není kategorie', NOW(), 'category-img.svg');

SELECT "Id", "Name", "Code", "Description", "DisplayOrder", "CreatedAt", "ImagePath"
FROM public."Categories";
