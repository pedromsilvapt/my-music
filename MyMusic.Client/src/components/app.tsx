import {AppShell, Burger, Divider, Group, NavLink} from "@mantine/core";
import {useDisclosure} from "@mantine/hooks";
import {
    IconDisc,
    IconHome,
    IconMusic,
    IconPlayerPlay,
    IconPlaylist,
    IconSettings,
    IconUsers
} from '@tabler/icons-react';
import '@mantine/core/styles.css';
import {Link, Outlet} from "@tanstack/react-router";
import {TanStackRouterDevtools} from "@tanstack/react-router-devtools";

function App() {
    const [mobileOpened, {toggle: toggleMobile}] = useDisclosure();
    const [desktopOpened, {toggle: toggleDesktop}] = useDisclosure(true);

    return (
        <AppShell
            header={{height: 60}}
            navbar={{width: 300, breakpoint: 'sm', collapsed: {mobile: !mobileOpened, desktop: !desktopOpened},}}
            padding="md"
        >
            <AppShell.Header>
                <Group h="100%" px="md">
                    <Burger opened={mobileOpened} onClick={toggleMobile} hiddenFrom="sm" size="sm" />
                    <Burger opened={desktopOpened} onClick={toggleDesktop} visibleFrom="sm" size="sm" />
                    <Group justify="space-between" style={{flex: 1}}>
                        MyMusic
                    </Group>
                </Group>
            </AppShell.Header>
            <AppShell.Navbar p="md">
                <NavLink
                    renderRoot={(props) => <Link to={"/player"} {...props} />}
                    href="/"
                    key="player"
                    leftSection={<IconPlayerPlay stroke={2} />}
                    label="Now Playing"
                />
                
                <Divider my="md" />
                
                <NavLink
                    renderRoot={(props) => <Link to={"/"} {...props} />}
                    key="home"
                    leftSection={<IconHome stroke={2} />}
                    label="Home"
                />
                <NavLink
                    renderRoot={(props) => <Link to={"/songs"} {...props} />}
                    href="/songs"
                    leftSection={<IconMusic stroke={2} />}
                    label="Songs"
                />
                <NavLink
                    renderRoot={(props) => <Link to={"/albums"} {...props} />}
                    key="albums"
                    leftSection={<IconDisc stroke={2} />}
                    label="Albums"
                />
                <NavLink
                    renderRoot={(props) => <Link to={"/artists"} {...props} />}
                    key="artists"
                    leftSection={<IconUsers stroke={2} />}
                    label="Artists"
                />
                <NavLink
                    renderRoot={(props) => <Link to={"/playlists"} {...props} />}
                    key="playlists"
                    leftSection={<IconPlaylist stroke={2} />}
                    label="Playlists"
                />
                <NavLink
                    renderRoot={(props) => <Link to={"/settings"} {...props} />}
                    key="settings"
                    leftSection={<IconSettings stroke={2} />}
                    label="Settings"
                />
            </AppShell.Navbar>
            <AppShell.Main style={{ '--parent-height': "calc(100vh - var(--app-shell-header-height, 0px) - var(--app-shell-footer-height, 0px) - var(--app-shell-padding) * 2)" }}>
                <Outlet />
                <TanStackRouterDevtools />
            </AppShell.Main>
        </AppShell>
    );
}

export default App
