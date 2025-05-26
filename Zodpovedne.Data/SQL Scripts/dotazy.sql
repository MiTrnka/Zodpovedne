-- Počet aktivnich spojeni na PostgreSQL
SELECT count(*) FROM pg_stat_activity;

-- Kdo lajkoval jakou diskuzi
SELECT 	"Users"."Nickname", "Discussions"."Title"
FROM 	"DiscussionLikes" inner join
		"Users" on "Users"."Id"="DiscussionLikes"."UserId" inner join
		"Discussions" on "Discussions"."Id" = "DiscussionLikes"."DiscussionId"
WHERE	"DiscussionLikes"."UserId" not in ('6f925769-14f0-41c8-b79f-135db37fc466','5cd9ed7f-9bbd-484e-95dd-5d96e6305cf5','a65603cb-089f-441a-8c39-6f2b1d92d804')

-- Kdo lajkoval jaky komentar
SELECT 	"Users"."Nickname", "Comments"."Content"
FROM 	"CommentLikes" inner join
		"Users" on "Users"."Id"="CommentLikes"."UserId" inner join
		"Comments" on "Comments"."Id" = "CommentLikes"."CommentId"
WHERE	"CommentLikes"."UserId" not in ('6f925769-14f0-41c8-b79f-135db37fc466','5cd9ed7f-9bbd-484e-95dd-5d96e6305cf5','a65603cb-089f-441a-8c39-6f2b1d92d804')

-- Poslední zprávy
SELECT 	sender."Nickname" odesilatel, recipient."Nickname" prijemce, "SentAt" odeslano, "ReadAt" precteno, "Content"
FROM 	"Messages" inner join
		"Users" sender on sender."Id" = "Messages"."SenderUserId" inner join
		"Users" recipient on recipient."Id" = "Messages"."RecipientUserId"
ORDER BY "Messages"."SentAt" DESC

-- Poslední komentáře
SELECT u."Nickname" ,c."ParentCommentId", c."Content", c."CreatedAt", c."UpdatedAt", c."Type"
FROM "Comments" c inner join "Users" u on c."UserId" = u."Id"
ORDER BY "CreatedAt" DESC

-- Poslední lajky diskuzí
SELECT 	u."Nickname", d."Title", dl."CreatedAt"
FROM 	"DiscussionLikes" dl inner join 
		"Users" u on dl."UserId" = u."Id" inner join
		"Discussions" d on dl."DiscussionId" = d."Id"
ORDER BY dl."CreatedAt" DESC

-- Poslední lajky komentářů
SELECT 	u."Nickname", c."Content", cl."CreatedAt"
FROM 	"CommentLikes" cl inner join 
		"Users" u on cl."UserId" = u."Id" inner join
		"Comments" c on cl."CommentId" = c."Id"
ORDER BY cl."CreatedAt" DESC