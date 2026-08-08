import {AppShell, Avatar, Burger, Divider, Group, NavLink, ScrollArea, Text} from "@mantine/core";
import {useDisclosure} from "@mantine/hooks";
import {
    IconClipboardCheck,
    IconDevices,
    IconDisc,
    IconHistory,
    IconHome,
    IconMusic,
    IconPlayerPlay,
    IconPlaylist,
    IconSettings,
    IconShare,
    IconShoppingCart,
    IconUser,
    IconUsers
} from '@tabler/icons-react';
import './styles';
import {Link, Outlet} from "@tanstack/react-router";
import {useTranslation} from "react-i18next";
import {useIsPlayerActive} from "../contexts/player-context.tsx";
import {usePlayerQueueInitializer} from "../hooks/use-player-queue-initializer";
import {useSharers} from "../hooks/use-sharers";
import {useUserPreferences} from "../hooks/use-user-preferences";
import ThemeToggle from "./common/theme-toggle.tsx";
import Player from "./player/player.tsx";
import PurchasesQueueIndicator from "./purchases/purchases-queue-indicator.tsx";

function App() {
    const {t} = useTranslation("common");
    const [mobileOpened, {toggle: toggleMobile}] = useDisclosure();
    const [desktopOpened, {toggle: toggleDesktop}] = useDisclosure(true);

    usePlayerQueueInitializer();
    const footerVisible = useIsPlayerActive();
    const {user} = useUserPreferences();
    const {sharers} = useSharers();

    return (
        <AppShell
            header={{height: 60}}
            navbar={{width: 300, breakpoint: 'sm', collapsed: {mobile: !mobileOpened, desktop: !desktopOpened},}}
            footer={{height: footerVisible ? 90 : 0}}
            padding="md"
        >
            <AppShell.Header data-testid="topbar">
                <Group h="100%" px="md">
                    <Burger opened={mobileOpened} onClick={toggleMobile} hiddenFrom="sm" size="sm" data-testid="topbar-mobile-burger"/>
                    <Burger opened={desktopOpened} onClick={toggleDesktop} visibleFrom="sm" size="sm" data-testid="topbar-desktop-burger"/>
                    <Group justify="space-between" style={{flex: 1}} data-testid="topbar-title">
                        {t("appName")}
                    </Group>
                    <Group gap="xs">
                        <Text size="sm" visibleFrom="sm" data-testid="topbar-username">{user.name}</Text>
                        <Avatar color="blue" radius="xl" size="sm" data-testid="topbar-avatar">
                            <IconUser size={16}/>
                        </Avatar>
                    </Group>
                    <ThemeToggle/>
                    <PurchasesQueueIndicator/>
                </Group>
            </AppShell.Header>
            <AppShell.Navbar data-testid="navbar">
                <ScrollArea h="100%" p="md">
                    <NavLink
                        data-testid="nav-player"
                        renderRoot={(props) => <Link to={"/player"} {...props} />}
                        href="/player"
                        key="player"
                        leftSection={<IconPlayerPlay stroke={2}/>}
                        label={t("nav.nowPlaying")}
                    />

                    <Divider my="md"/>

                    <NavLink
                        data-testid="nav-home"
                        renderRoot={(props) => <Link to={"/"} {...props} />}
                        key="home"
                        leftSection={<IconHome stroke={2}/>}
                        label={t("nav.home")}
                    />
                    <NavLink
                        data-testid="nav-songs"
                        renderRoot={(props) => <Link to={"/songs"} {...props} />}
                        href="/songs"
                        leftSection={<IconMusic stroke={2}/>}
                        label={t("nav.songs")}
                        children={sharers.length > 0 ? (
                            <>
                                <NavLink
                                    data-testid="nav-songs-mine"
                                    renderRoot={(props) => <Link to={"/songs"} activeOptions={{exact: true}} {...props} />}
                                    label={t("nav.songsMine")}
                                    leftSection={<IconUser size={16}/>}
                                    style={{paddingLeft: "2rem"}}
                                />
                                {sharers.map((sharer) => (
                                    <NavLink
                                        key={sharer.id}
                                        data-testid={`nav-songs-shared-${sharer.id}`}
                                        renderRoot={(props) => <Link to={"/songs/shared/$ownerId"} params={{ownerId: String(sharer.id)}} {...props} />}
                                        label={sharer.name}
                                        leftSection={<IconShare size={16}/>}
                                        style={{paddingLeft: "2rem"}}
                                    />
                                ))}
                            </>
                        ) : undefined}
                    />
                    <NavLink
                        data-testid="nav-albums"
                        renderRoot={(props) => <Link to={"/albums"} {...props} />}
                        key="albums"
                        leftSection={<IconDisc stroke={2}/>}
                        label={t("nav.albums")}
                    />
                    <NavLink
                        data-testid="nav-artists"
                        renderRoot={(props) => <Link to={"/artists"} {...props} />}
                        key="artists"
                        leftSection={<IconUsers stroke={2}/>}
                        label={t("nav.artists")}
                    />
                    <NavLink
                        data-testid="nav-playlists"
                        renderRoot={(props) => <Link to={"/playlists"} {...props} />}
                        key="playlists"
                        leftSection={<IconPlaylist stroke={2}/>}
                        label={t("nav.playlists")}
                    />
                    <NavLink
                        data-testid="nav-devices"
                        renderRoot={(props) => <Link to={"/devices"} {...props} />}
                        key="devices"
                        leftSection={<IconDevices stroke={2}/>}
                        label={t("nav.devices")}
                    />
                    <NavLink
                        data-testid="nav-history"
                        renderRoot={(props) => <Link to={"/history"} {...props} />}
                        key="history"
                        leftSection={<IconHistory stroke={2}/>}
                        label={t("nav.history")}
                    />
                    <NavLink
                        data-testid="nav-audits"
                        renderRoot={(props) => <Link to={"/audits"} {...props} />}
                        key="audits"
                        leftSection={<IconClipboardCheck stroke={2}/>}
                        label={t("nav.audits")}
                    />
                    <NavLink
                        data-testid="nav-purchases"
                        renderRoot={(props) => <Link to={"/purchases"} {...props} />}
                        key="purchases"
                        leftSection={<IconShoppingCart stroke={2}/>}
                        label={t("nav.purchases")}
                    />
                    <NavLink
                        data-testid="nav-settings"
                        renderRoot={(props) => <Link to={"/settings"} {...props} />}
                        key="settings"
                        leftSection={<IconSettings stroke={2}/>}
                        label={t("nav.settings")}
                    />
                </ScrollArea>
            </AppShell.Navbar>
            <AppShell.Main
                style={{'--parent-height': "calc(100vh - var(--app-shell-header-height, 0px) - var(--app-shell-footer-height, 0px) - var(--app-shell-padding) * 2)"}}>
                <Outlet/>

                {/*<TanStackRouterDevtools/>*/}
            </AppShell.Main>
            {footerVisible && <AppShell.Footer>
                <Player/>
            </AppShell.Footer>}
        </AppShell>
    );
}

export default App
