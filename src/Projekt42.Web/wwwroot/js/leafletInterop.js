window.leafletInterop = {
    maps: {},

    initMap: function (mapId, lat, lon, zoom) {
        if (this.maps[mapId]) {
            this.maps[mapId].remove();
            delete this.maps[mapId];
        }
        var map = L.map(mapId).setView([lat, lon], zoom);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        }).addTo(map);
        this.maps[mapId] = map;
    },

    addMarker: function (mapId, lat, lon, date, hasThumbnail, imageId, topicId, count) {
        var map = this.maps[mapId];
        if (!map) return;

        var multi = count > 1;
        var albumHref = topicId > 0 ? '/album/' + topicId : '/';
        var btnLabel = multi ? 'Album öffnen' : 'Bild anzeigen';
        var baseHref, onclick;
        if (multi) {
            baseHref = albumHref;
            onclick = '';
        } else {
            baseHref = albumHref + (albumHref.indexOf('?') < 0 ? '?' : '&')
                + 'openImage=' + imageId + '&fromMap=1&imgLat=' + lat + '&imgLon=' + lon + '&mapZoom=13';
            onclick = 'this.href=this.href.replace(/mapZoom=\\d+/,"mapZoom="+leafletInterop.maps["' + mapId + '"].getZoom());';
        }

        var popup = '<div style="text-align:center;width:160px;">';
        if (hasThumbnail) {
            popup += '<div style="position:relative;display:inline-block;">';
            popup += '<a href="' + baseHref + '" onclick="' + onclick + '"><img src="/images/thumbnail/' + imageId + '" style="width:160px;height:120px;object-fit:cover;border-radius:4px;display:block;" /></a>';
            if (multi) {
                popup += '<span style="position:absolute;top:4px;right:4px;background:rgba(0,0,0,0.65);color:#fff;font-size:0.75rem;padding:2px 6px;border-radius:10px;">' + count + ' Fotos</span>';
            }
            popup += '</div>';
        }
        popup += '<div style="font-size:0.85rem;margin-top:0.4rem">' + date + '</div>';
        popup += '<a href="' + baseHref + '" onclick="' + onclick + '" class="btn btn-sm btn-primary mt-1" style="color:#fff!important">' + btnLabel + '</a>';
        popup += '</div>';

        L.marker([lat, lon])
            .addTo(map)
            .bindPopup(popup, { maxWidth: 200 });
    },

    fitBounds: function (mapId, markers) {
        var map = this.maps[mapId];
        if (!map || !markers || markers.length === 0) return;
        var bounds = L.latLngBounds(markers.map(function (m) { return [m.lat, m.lon]; }));
        map.fitBounds(bounds, { padding: [30, 30] });
    }
};
