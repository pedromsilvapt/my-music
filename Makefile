.PHONY: patch minor major retag format format-check

patch minor major retag:
	@./scripts/bump.sh $@

# Formatters per language. TypeScript (Client/Mobile) has no formatter wired up yet: add it here.
format:
	dotnet format whitespace MyMusic.sln
	dotnet format style MyMusic.sln --no-restore

format-check:
	dotnet format whitespace MyMusic.sln --verify-no-changes
	dotnet format style MyMusic.sln --no-restore --verify-no-changes
