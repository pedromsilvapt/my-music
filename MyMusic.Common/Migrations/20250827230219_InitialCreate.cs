using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "artworks",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    data = table.Column<byte[]>(type: "bytea", nullable: false),
                    mime_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artworks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sources",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    icon = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    address = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    is_paid = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "artists",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    photo_id = table.Column<long>(type: "bigint", nullable: true),
                    background_id = table.Column<long>(type: "bigint", nullable: true),
                    owner_id = table.Column<long>(type: "bigint", nullable: false),
                    songs_count = table.Column<int>(type: "integer", nullable: false),
                    albums_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artists", x => x.id);
                    table.ForeignKey(
                        name: "fk_artists_artworks_background_id",
                        column: x => x.background_id,
                        principalTable: "artworks",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_artists_artworks_photo_id",
                        column: x => x.photo_id,
                        principalTable: "artworks",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_artists_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    owner_id = table.Column<long>(type: "bigint", nullable: false),
                    icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    last_sync_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_devices", x => x.id);
                    table.ForeignKey(
                        name: "fk_devices_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "genres",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    owner_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_genres", x => x.id);
                    table.ForeignKey(
                        name: "fk_genres_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "albums",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    cover_id = table.Column<long>(type: "bigint", nullable: true),
                    year = table.Column<int>(type: "integer", nullable: true),
                    artist_id = table.Column<long>(type: "bigint", nullable: false),
                    owner_id = table.Column<long>(type: "bigint", nullable: false),
                    songs_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_albums", x => x.id);
                    table.ForeignKey(
                        name: "fk_albums_artists_artist_id",
                        column: x => x.artist_id,
                        principalTable: "artists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_albums_artworks_cover_id",
                        column: x => x.cover_id,
                        principalTable: "artworks",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_albums_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "artist_sources",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    artist_id = table.Column<long>(type: "bigint", nullable: false),
                    source_id1 = table.Column<int>(type: "integer", nullable: false),
                    source_id = table.Column<long>(type: "bigint", nullable: false),
                    external_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artist_sources", x => x.id);
                    table.ForeignKey(
                        name: "fk_artist_sources_artists_artist_id",
                        column: x => x.artist_id,
                        principalTable: "artists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_artist_sources_sources_source_id1",
                        column: x => x.source_id1,
                        principalTable: "sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "album_sources",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    album_id = table.Column<long>(type: "bigint", nullable: false),
                    source_id1 = table.Column<int>(type: "integer", nullable: false),
                    source_id = table.Column<long>(type: "bigint", nullable: false),
                    external_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    songs_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_album_sources", x => x.id);
                    table.ForeignKey(
                        name: "fk_album_sources_albums_album_id",
                        column: x => x.album_id,
                        principalTable: "albums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_album_sources_sources_source_id1",
                        column: x => x.source_id1,
                        principalTable: "sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "songs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    label = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    album_id = table.Column<long>(type: "bigint", nullable: false),
                    cover_id = table.Column<long>(type: "bigint", nullable: true),
                    year = table.Column<int>(type: "integer", nullable: true),
                    lyrics = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: true),
                    @explicit = table.Column<bool>(name: "explicit", type: "boolean", nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    track = table.Column<int>(type: "integer", nullable: true),
                    duration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    owner_id = table.Column<long>(type: "bigint", nullable: false),
                    rating = table.Column<decimal>(type: "numeric", nullable: true),
                    repository_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    checksum = table.Column<string>(type: "character varying(88)", maxLength: 88, nullable: false),
                    checksum_algorithm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_songs", x => x.id);
                    table.ForeignKey(
                        name: "fk_songs_albums_album_id",
                        column: x => x.album_id,
                        principalTable: "albums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_songs_artworks_cover_id",
                        column: x => x.cover_id,
                        principalTable: "artworks",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_songs_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "song_artists",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    song_id = table.Column<long>(type: "bigint", nullable: false),
                    artist_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_song_artists", x => x.id);
                    table.ForeignKey(
                        name: "fk_song_artists_artists_artist_id",
                        column: x => x.artist_id,
                        principalTable: "artists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_song_artists_songs_song_id",
                        column: x => x.song_id,
                        principalTable: "songs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "song_devices",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    song_id = table.Column<long>(type: "bigint", nullable: false),
                    device_id = table.Column<long>(type: "bigint", nullable: false),
                    device_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    sync_action = table.Column<int>(type: "integer", nullable: true),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_song_devices", x => x.id);
                    table.ForeignKey(
                        name: "fk_song_devices_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_song_devices_songs_song_id",
                        column: x => x.song_id,
                        principalTable: "songs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "song_genres",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    song_id = table.Column<long>(type: "bigint", nullable: false),
                    genre_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_song_genres", x => x.id);
                    table.ForeignKey(
                        name: "fk_song_genres_genres_genre_id",
                        column: x => x.genre_id,
                        principalTable: "genres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_song_genres_songs_song_id",
                        column: x => x.song_id,
                        principalTable: "songs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "song_sources",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    song_id = table.Column<long>(type: "bigint", nullable: false),
                    source_id1 = table.Column<int>(type: "integer", nullable: false),
                    source_id = table.Column<long>(type: "bigint", nullable: false),
                    external_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_song_sources", x => x.id);
                    table.ForeignKey(
                        name: "fk_song_sources_songs_song_id",
                        column: x => x.song_id,
                        principalTable: "songs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_song_sources_sources_source_id1",
                        column: x => x.source_id1,
                        principalTable: "sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_album_sources_album_id",
                table: "album_sources",
                column: "album_id");

            migrationBuilder.CreateIndex(
                name: "ix_album_sources_source_id1",
                table: "album_sources",
                column: "source_id1");

            migrationBuilder.CreateIndex(
                name: "ix_albums_artist_id",
                table: "albums",
                column: "artist_id");

            migrationBuilder.CreateIndex(
                name: "ix_albums_cover_id",
                table: "albums",
                column: "cover_id");

            migrationBuilder.CreateIndex(
                name: "ix_albums_owner_id_artist_id_name",
                table: "albums",
                columns: new[] { "owner_id", "artist_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_artist_sources_artist_id",
                table: "artist_sources",
                column: "artist_id");

            migrationBuilder.CreateIndex(
                name: "ix_artist_sources_source_id1",
                table: "artist_sources",
                column: "source_id1");

            migrationBuilder.CreateIndex(
                name: "ix_artists_background_id",
                table: "artists",
                column: "background_id");

            migrationBuilder.CreateIndex(
                name: "ix_artists_owner_id",
                table: "artists",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_artists_photo_id",
                table: "artists",
                column: "photo_id");

            migrationBuilder.CreateIndex(
                name: "ix_devices_owner_id_name",
                table: "devices",
                columns: new[] { "owner_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_genres_owner_id_name",
                table: "genres",
                columns: new[] { "owner_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_song_artists_artist_id",
                table: "song_artists",
                column: "artist_id");

            migrationBuilder.CreateIndex(
                name: "ix_song_artists_song_id_artist_id",
                table: "song_artists",
                columns: new[] { "song_id", "artist_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_song_devices_device_id_device_path",
                table: "song_devices",
                columns: new[] { "device_id", "device_path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_song_devices_song_id",
                table: "song_devices",
                column: "song_id");

            migrationBuilder.CreateIndex(
                name: "ix_song_genres_genre_id",
                table: "song_genres",
                column: "genre_id");

            migrationBuilder.CreateIndex(
                name: "ix_song_genres_song_id_genre_id",
                table: "song_genres",
                columns: new[] { "song_id", "genre_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_song_sources_song_id",
                table: "song_sources",
                column: "song_id");

            migrationBuilder.CreateIndex(
                name: "ix_song_sources_source_id1",
                table: "song_sources",
                column: "source_id1");

            migrationBuilder.CreateIndex(
                name: "ix_songs_album_id",
                table: "songs",
                column: "album_id");

            migrationBuilder.CreateIndex(
                name: "ix_songs_cover_id",
                table: "songs",
                column: "cover_id");

            migrationBuilder.CreateIndex(
                name: "ix_songs_owner_id",
                table: "songs",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "album_sources");

            migrationBuilder.DropTable(
                name: "artist_sources");

            migrationBuilder.DropTable(
                name: "song_artists");

            migrationBuilder.DropTable(
                name: "song_devices");

            migrationBuilder.DropTable(
                name: "song_genres");

            migrationBuilder.DropTable(
                name: "song_sources");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "genres");

            migrationBuilder.DropTable(
                name: "songs");

            migrationBuilder.DropTable(
                name: "sources");

            migrationBuilder.DropTable(
                name: "albums");

            migrationBuilder.DropTable(
                name: "artists");

            migrationBuilder.DropTable(
                name: "artworks");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
