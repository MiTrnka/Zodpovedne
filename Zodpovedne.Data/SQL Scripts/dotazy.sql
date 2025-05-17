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