namespace MineMart.Data {
    /// <summary>
    /// Vai trò nghề nghiệp của nhân viên (Worker Role).
    /// </summary>
    public enum WorkerRole {
        Miner = 0,         // Thợ Đào: Khai thác mỏ -> Bỏ quặng lên bàn trung chuyển
        Runner = 1,        // Thợ Vận Chuyển: Nhận order -> Bê quặng từ bàn trung chuyển ra quầy/bàn giao cho khách
        DirectSeller = 2   // Thợ Đa Năng / Bán Trực Tiếp: Tự nhận order -> Tự đào -> Tự giao tận tay khách
    }
}
