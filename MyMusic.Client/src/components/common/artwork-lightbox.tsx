import {Box, Overlay, Portal} from "@mantine/core";
import {useCallback, useEffect, useRef, useState} from "react";
import {RemoveScroll} from "react-remove-scroll";

interface ArtworkLightboxProps {
    opened: boolean;
    onClose: () => void;
    src: string;
}

type ZoomLevel = "fit" | 100;

export default function ArtworkLightbox(props: ArtworkLightboxProps) {
    const {opened, onClose, src} = props;

    const [zoom, setZoom] = useState<ZoomLevel>("fit");
    const [position, setPosition] = useState({x: 0, y: 0});
    const [isDragging, setIsDragging] = useState(false);
    const [imgSize, setImgSize] = useState({width: 0, height: 0});
    const dragStart = useRef({x: 0, y: 0});
    const isDraggingRef = useRef(false);
    const mousePos = useRef({x: 0, y: 0});
    const dragDistance = useRef(0);
    const imgRef = useRef<HTMLImageElement>(null);

    const handleImgLoad = (e: React.SyntheticEvent<HTMLImageElement>) => {
        const img = e.currentTarget;
        setImgSize({width: img.naturalWidth, height: img.naturalHeight});
    };

    const calculateFitScale = useCallback(() => {
        if (imgSize.width === 0 || imgSize.height === 0) return 1;

        const viewportWidth = window.innerWidth;
        const viewportHeight = window.innerHeight;

        const scaleX = (viewportWidth * 0.9) / imgSize.width;
        const scaleY = (viewportHeight * 0.9) / imgSize.height;

        return Math.min(scaleX, scaleY, 1);
    }, [imgSize]);

    const handleWheel = useCallback((e: React.WheelEvent) => {
        e.preventDefault();

        const img = imgRef.current;
        if (!img) return;

        const rect = img.getBoundingClientRect();
        const centerX = rect.left + rect.width / 2;
        const centerY = rect.top + rect.height / 2;

        mousePos.current = {x: e.clientX, y: e.clientY};

        const fitScale = calculateFitScale();

        setZoom(current => {
            if (current === "fit") {
                const mouseOffsetX = e.clientX - centerX;
                const mouseOffsetY = e.clientY - centerY;
                setPosition({
                    x: -mouseOffsetX,
                    y: -mouseOffsetY
                });
                return 100;
            }

            const oldScale = (current / 100) * fitScale;
            const delta = e.deltaY > 0 ? -25 : 25;
            const newZoom = Math.max(25, Math.min(400, current + delta));
            const newScale = (newZoom / 100) * fitScale;

            const offsetX = mousePos.current.x - centerX;
            const offsetY = mousePos.current.y - centerY;

            const newPos = {
                x: position.x + offsetX * (1 - newScale / oldScale),
                y: position.y + offsetY * (1 - newScale / oldScale)
            };

            setPosition(newPos);
            return newZoom as ZoomLevel;
        });
    }, [position, calculateFitScale]);

    const handleMouseDown = (e: React.MouseEvent) => {
        if (e.button !== 0 || zoom === "fit") return;
        isDraggingRef.current = true;
        setIsDragging(true);
        dragDistance.current = 0;
        dragStart.current = {
            x: e.clientX - position.x,
            y: e.clientY - position.y
        };
        e.preventDefault();
        e.stopPropagation();
    };

    const handleImageClick = () => {
        if (dragDistance.current > 5) {
            dragDistance.current = 0;
            return;
        }
        dragDistance.current = 0;

        const img = imgRef.current;
        if (!img) return;

        const rect = img.getBoundingClientRect();
        const centerX = rect.left + rect.width / 2;
        const centerY = rect.top + rect.height / 2;

        setZoom(current => {
            const newZoom = current === "fit" ? 100 : "fit";

            if (newZoom === "fit") {
                setPosition({x: 0, y: 0});
            } else if (current === "fit") {
                const mouseOffsetX = mousePos.current.x - centerX;
                const mouseOffsetY = mousePos.current.y - centerY;
                setPosition({
                    x: -mouseOffsetX,
                    y: -mouseOffsetY
                });
            }

            return newZoom;
        });
    };

    const handleMouseMove = useCallback((e: MouseEvent) => {
        mousePos.current = {x: e.clientX, y: e.clientY};

        if (!isDraggingRef.current) return;

        const dx = e.clientX - dragStart.current.x + position.x;
        const dy = e.clientY - dragStart.current.y + position.y;
        dragDistance.current = Math.sqrt(dx * dx + dy * dy);

        setPosition({
            x: e.clientX - dragStart.current.x,
            y: e.clientY - dragStart.current.y
        });
    }, [position]);

    const handleMouseUp = useCallback(() => {
        isDraggingRef.current = false;
        setIsDragging(false);
    }, []);

    const handleClose = () => {
        setZoom("fit");
        setPosition({x: 0, y: 0});
        onClose();
    };

    const getTransform = () => {
        const fitScale = calculateFitScale();
        const scale = zoom === "fit" ? fitScale : (zoom / 100);
        return `translate(${position.x}px, ${position.y}px) scale(${scale})`;
    };

    useEffect(() => {
        if (!opened) return;

        document.addEventListener("mousemove", handleMouseMove);
        document.addEventListener("mouseup", handleMouseUp);

        return () => {
            document.removeEventListener("mousemove", handleMouseMove);
            document.removeEventListener("mouseup", handleMouseUp);
        };
    }, [opened, handleMouseMove, handleMouseUp]);

    if (!opened) return null;

    return (
        <Portal>
            <RemoveScroll enabled={opened}>
                <Overlay
                    backgroundOpacity={0.55}
                    fixed
                    onClick={handleClose}
                    style={{zIndex: 1000}}
                />
                <Box
                    style={{
                        position: "fixed",
                        top: 0,
                        left: 0,
                        right: 0,
                        bottom: 0,
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        zIndex: 1001,
                        pointerEvents: "none"
                    }}
                >
                    {/* eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions */}
                    <div
                        onClick={handleImageClick}
                        onMouseDown={handleMouseDown}
                        onWheel={handleWheel}
                        style={{
                            background: "none",
                            border: "none",
                            padding: 0,
                            margin: 0,
                            cursor: isDragging ? "grabbing" : zoom === "fit" ? "zoom-in" : "grab",
                            pointerEvents: "auto",
                            transform: getTransform(),
                            transition: isDragging ? "none" : "transform 0.1s ease-out"
                        }}
                    >
                        <img
                            ref={imgRef}
                            src={src}
                            alt="Artwork preview"
                            onLoad={handleImgLoad}
                            style={{
                                maxWidth: "none",
                                maxHeight: "none",
                                userSelect: "none",
                                pointerEvents: "none"
                            }}
                        />
                    </div>
                </Box>
            </RemoveScroll>
        </Portal>
    );
}
