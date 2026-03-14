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

    addMarker: function (mapId, lat, lon, title, thumbnailPath, imageId) {
        var map = this.maps[mapId];
        if (!map) return;

        var popup = '<div style="text-align:center">';
        if (thumbnailPath) {
            popup += '<img src="/images/thumbnail/' + imageId + '" style="max-width:200px;max-height:150px;" /><br/>';
        }
        popup += '<strong>' + title + '</strong>';
        popup += '<br/><a href="/image/' + imageId + '">View image</a>';
        popup += '</div>';

        L.marker([lat, lon])
            .addTo(map)
            .bindPopup(popup);
    },

    fitBounds: function (mapId, markers) {
        var map = this.maps[mapId];
        if (!map || !markers || markers.length === 0) return;
        var bounds = L.latLngBounds(markers.map(function (m) { return [m.lat, m.lon]; }));
        map.fitBounds(bounds, { padding: [30, 30] });
    }
};
