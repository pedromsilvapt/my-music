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
    IconShoppingCart,
    IconUser,
    IconUsers
} from '@tabler/icons-react';
import './styles';
import {Link, Outlet} from "@tanstack/react-router";
import {useIsPlayerActive} from "../contexts/player-context.tsx";
import {usePlayerQueueInitializer} from "../hooks/use-player-queue-initializer";
import {useUserPreferences} from "../hooks/use-user-preferences";
import ThemeToggle from "./common/theme-toggle.tsx";
import Player from "./player/player.tsx";
import PurchasesQueueIndicator from "./purchases/purchases-queue-indicator.tsx";

function App() {
    const [mobileOpened, {toggle: toggleMobile}] = useDisclosure();
    const [desktopOpened, {toggle: toggleDesktop}] = useDisclosure(true);

    usePlayerQueueInitializer();
    const footerVisible = useIsPlayerActive();
    const {user} = useUserPreferences();

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
                        MyMusic
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
                        label="Now Playing"
                    />

                    <Divider my="md"/>

                    <NavLink
                        data-testid="nav-home"
                        renderRoot={(props) => <Link to={"/"} {...props} />}
                        key="home"
                        leftSection={<IconHome stroke={2}/>}
                        label="Home"
                    />
                    <NavLink
                        data-testid="nav-songs"
                        renderRoot={(props) => <Link to={"/songs"} {...props} />}
                        href="/songs"
                        leftSection={<IconMusic stroke={2}/>}
                        label="Songs"
                    />
                    <NavLink
                        data-testid="nav-albums"
                        renderRoot={(props) => <Link to={"/albums"} {...props} />}
                        key="albums"
                        leftSection={<IconDisc stroke={2}/>}
                        label="Albums"
                    />
                    <NavLink
                        data-testid="nav-artists"
                        renderRoot={(props) => <Link to={"/artists"} {...props} />}
                        key="artists"
                        leftSection={<IconUsers stroke={2}/>}
                        label="Artists"
                    />
                    <NavLink
                        data-testid="nav-playlists"
                        renderRoot={(props) => <Link to={"/playlists"} {...props} />}
                        key="playlists"
                        leftSection={<IconPlaylist stroke={2}/>}
                        label="Playlists"
                    />
                    <NavLink
                        data-testid="nav-devices"
                        renderRoot={(props) => <Link to={"/devices"} {...props} />}
                        key="devices"
                        leftSection={<IconDevices stroke={2}/>}
                        label="Devices"
                    />
                    <NavLink
                        data-testid="nav-history"
                        renderRoot={(props) => <Link to={"/history"} {...props} />}
                        key="history"
                        leftSection={<IconHistory stroke={2}/>}
                        label="History"
                    />
                    <NavLink
                        data-testid="nav-audits"
                        renderRoot={(props) => <Link to={"/audits"} {...props} />}
                        key="audits"
                        leftSection={<IconClipboardCheck stroke={2}/>}
                        label="Audits"
                    />
                    <NavLink
                        data-testid="nav-purchases"
                        renderRoot={(props) => <Link to={"/purchases"} {...props} />}
                        key="purchases"
                        leftSection={<IconShoppingCart stroke={2}/>}
                        label="Purchases"
                    />
                    <NavLink
                        data-testid="nav-settings"
                        renderRoot={(props) => <Link to={"/settings"} {...props} />}
                        key="settings"
                        leftSection={<IconSettings stroke={2}/>}
                        label="Settings"
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
