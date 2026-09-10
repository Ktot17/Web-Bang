-- create user usual_user;
-- GRANT CONNECT ON DATABASE "bangtest" TO usual_user;
-- GRANT USAGE ON SCHEMA decks TO usual_user;
-- grant select on decks.classicdeck to usual_user;
-- alter user usual_user with password '1';

-- create schema decks;
-- create table decks.classicdeck (name int, suit int, rank int); 

-- Bang (0) - Spades (2) Ace (12)
insert into decks.classicdeck(name, suit, rank) values (0, 2, 12);

-- Bang (0) - Diamonds (0) для всех рангов от Two (0) до Ace (12)
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 0, 0);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 0, 1);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 0, 2);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 0, 3);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 0, 4);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 0, 5);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 0, 6);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 0, 7);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 0, 8);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 0, 9);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 0, 10);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 0, 11);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 0, 12);

-- Bang (0) - Clubs (3) для рангов Two (0) до Nine (7)
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 3, 0);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 3, 1);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 3, 2);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 3, 3);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 3, 4);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 3, 5);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 3, 6);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 3, 7);

-- Bang (0) - Hearts (1) для рангов Queen (10) до Ace (12)
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 1, 10);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 1, 11);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (0, 1, 12);

-- Beer (1) - Hearts (1) для рангов Six (4) до Jack (9)
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (1, 1, 4);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (1, 1, 5);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (1, 1, 6);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (1, 1, 7);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (1, 1, 8);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (1, 1, 9);

-- Missed (2) - Clubs (3) для рангов Ten (8) до Ace (12)
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (2, 3, 8);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (2, 3, 9);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (2, 3, 10);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (2, 3, 11);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (2, 3, 12);

-- Missed (2) - Spades (2) для рангов Two (0) до Eight (6)
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (2, 2, 0);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (2, 2, 1);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (2, 2, 2);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (2, 2, 3);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (2, 2, 4);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (2, 2, 5);
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (2, 2, 6);

-- Одиночные карты
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (3, 1, 9);   -- Panic
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (3, 1, 10);  -- Panic
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (3, 1, 12);  -- Panic
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (3, 0, 6);   -- Panic
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (4, 3, 7);   -- GeneralStore
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (4, 2, 10);  -- GeneralStore
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (5, 0, 11);  -- Indians
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (5, 0, 12);  -- Indians
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (6, 0, 10);  -- Duel
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (6, 2, 9);   -- Duel
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (6, 3, 6);   -- Duel
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (7, 1, 8);   -- Gatling
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (8, 1, 11);  -- CatBalou
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (8, 0, 7);   -- CatBalou
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (8, 0, 8);   -- CatBalou
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (8, 0, 9);   -- CatBalou
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (9, 1, 3);   -- Saloon
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (10, 2, 7);  -- Stagecoach
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (10, 2, 7);  -- Stagecoach
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (11, 1, 1);  -- WellsFargo
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (12, 2, 10); -- Barrel
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (12, 2, 11); -- Barrel
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (13, 2, 12); -- Scope
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (14, 1, 6);  -- Mustang
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (14, 1, 7);  -- Mustang
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (15, 1, 0);  -- Dynamite
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (16, 1, 2);  -- BeerBarrel
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (17, 2, 8);  -- Jail
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (17, 2, 9);  -- Jail
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (18, 2, 8);  -- Volcanic
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (18, 3, 8);  -- Volcanic
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (19, 3, 9);  -- Schofield
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (19, 3, 10); -- Schofield
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (19, 2, 11); -- Schofield
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (20, 3, 11);  -- Remington
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (21, 3, 12);  -- Carabine
INSERT INTO decks.classicdeck (name, suit, rank) VALUES (22, 2, 6);  -- Winchester