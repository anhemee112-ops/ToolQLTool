package org.microemu.app;

import java.awt.Toolkit;
import java.awt.event.ComponentAdapter;
import java.awt.event.ComponentEvent;
import java.awt.event.WindowAdapter;
import java.awt.event.WindowEvent;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.List;
import javax.microedition.lcdui.Displayable;
import javax.swing.JFrame;
import javax.swing.UIManager;
import org.microemu.DisplayAccess;
import org.microemu.DisplayComponent;
import org.microemu.EmulatorContext;
import org.microemu.MIDletAccess;
import org.microemu.MIDletBridge;
import org.microemu.app.ui.swing.SwingDeviceComponent;
import org.microemu.app.ui.swing.SwingDisplayComponent;
import org.microemu.app.ui.swing.SwingSelectDevicePanel;
import org.microemu.app.util.DeviceEntry;
import org.microemu.device.DeviceDisplay;
import org.microemu.device.DeviceFactory;
import org.microemu.device.FontManager;
import org.microemu.device.InputMethod;
import org.microemu.device.impl.DeviceDisplayImpl;
import org.microemu.device.impl.Rectangle;
import org.microemu.device.j2se.J2SEDevice;
import org.microemu.device.j2se.J2SEDeviceDisplay;
import org.microemu.device.j2se.J2SEFontManager;
import org.microemu.device.j2se.J2SEInputMethod;

/**
 * Cửa sổ chính của AngelAChip bản gọn (ToolQL): chỉ còn khung chứa màn hình game.
 * Không có thanh menu, thanh trạng thái, hộp thoại, cửa sổ nhật ký, quản lý kho RMS, ghi GIF...
 * Tính năng duy nhất giữ lại là "tự động ngủ" (xem {@link Displayable#enableAutoSleep}), luôn bật.
 */
public class Main extends JFrame {
    public static final String TITLE = "AngelChip Emulator";

    public Common common;
    public SwingSelectDevicePanel selectDevicePanel;
    public EmulatorContext emulatorContext;

    private final SwingDeviceComponent deviceComponent = new SwingDeviceComponent();

    /** Ngữ cảnh trình giả lập: nối màn hình, bàn phím/chuột và phông chữ kiểu J2SE với khung này. */
    private final class Context implements EmulatorContext {
        private final InputMethod inputMethod = new J2SEInputMethod();
        private final DeviceDisplay deviceDisplay = new J2SEDeviceDisplay(this);
        private final FontManager fontManager = new J2SEFontManager();

        public DisplayComponent getDisplayComponent() {
            return deviceComponent.getDisplayComponent();
        }

        public InputMethod getDeviceInputMethod() {
            return inputMethod;
        }

        public DeviceDisplay getDeviceDisplay() {
            return deviceDisplay;
        }

        public FontManager getDeviceFontManager() {
            return fontManager;
        }

        public InputStream getResourceAsStream(String name) {
            return MIDletBridge.getCurrentMIDlet().getClass().getResourceAsStream(name);
        }

        /** Không mở trình duyệt theo yêu cầu của game. */
        public boolean platformRequest(String url) {
            return false;
        }
    }

    public Main() {
        emulatorContext = new Context();
        setTitle(TITLE);
        setIconImage(Toolkit.getDefaultToolkit().getImage(Main.class.getResource("/org/microemu/icon.png")));
        setDefaultCloseOperation(JFrame.DO_NOTHING_ON_CLOSE);
        addWindowListener(new WindowAdapter() {
            public void windowClosing(WindowEvent e) {
                System.exit(0);
            }

            // Thu nhỏ cửa sổ (Ẩn client) thì tạm dừng game để đỡ tốn CPU; hiện lại thì chạy tiếp.
            public void windowIconified(WindowEvent e) {
                try {
                    MIDletBridge.getMIDletAccess(MIDletBridge.getCurrentMIDlet()).pauseApp();
                } catch (RuntimeException ex) {
                    System.err.println(ex);
                }
            }

            public void windowDeiconified(WindowEvent e) {
                try {
                    MIDletBridge.getMIDletAccess(MIDletBridge.getCurrentMIDlet()).startApp();
                } catch (Exception ex) {
                    System.err.println(ex);
                }
            }
        });

        Config.loadConfig(null, emulatorContext);
        Rectangle location = Config.getWindow("main", new Rectangle(0, 0, 160, 120));
        setLocation(location.x, location.y);
        getContentPane().add(deviceComponent, "Center");
        deviceComponent.addKeyListener(deviceComponent);
        addKeyListener(deviceComponent);

        selectDevicePanel = new SwingSelectDevicePanel(emulatorContext);
        common = new Common(emulatorContext);
        common.loadImplementationsFromConfig();
    }

    /** Giải chuỗi mã hóa (lớp <c>a</c>, màn hình ngủ, gọi hàm này). Trả null nếu tổng kiểm sai. */
    public static String int2str(int[] values) {
        int n = values.length - 1;
        int sum = 0;
        StringBuilder text = new StringBuilder();
        for (int i = 0; i < n; i++) {
            sum += values[i];
            text.append((char) (values[i] - n));
        }
        return (sum - values[n]) % n == 0 ? text.toString() : null;
    }

    protected void updateDevice() {
        deviceComponent.init();
        boolean resizable = ((DeviceDisplayImpl) DeviceFactory.getDevice().getDeviceDisplay()).isResizable();
        setResizable(resizable);
        pack();
        deviceComponent.requestFocus();
    }

    public static void main(String[] args) {
        List<String> arguments = new ArrayList<>();
        for (String arg : args) arguments.add(arg);

        try {
            UIManager.setLookAndFeel(UIManager.getSystemLookAndFeelClassName());
        } catch (Exception e) {
            org.microemu.log.Logger.error(e);
        }

        final Main app = new Main();
        DeviceEntry entry = app.selectDevicePanel.getSelectedDeviceEntry();
        if (app.common.initParams(arguments, entry, J2SEDevice.class)) {
            DeviceDisplayImpl display = (DeviceDisplayImpl) DeviceFactory.getDevice().getDeviceDisplay();
            if (display.isResizable()) {
                Rectangle size = Config.getDeviceEntryDisplaySize(entry);
                if (size != null) display.setDisplayRectangle(size);
            }
        }
        app.updateDevice();
        app.validate();
        app.setVisible(true);

        // Tự động ngủ: luôn bật, không có công tắc.
        Displayable.enableAutoSleep = true;

        app.common.initMIDlet(false);

        // Kéo giãn cửa sổ (thiết bị đổi kích thước được) thì cập nhật vùng vẽ của game.
        app.addComponentListener(new ComponentAdapter() {
            public void componentResized(ComponentEvent e) {
                DeviceDisplayImpl display = (DeviceDisplayImpl) DeviceFactory.getDevice().getDeviceDisplay();
                if (!display.isResizable()) return;
                display.setDisplayRectangle(new Rectangle(0, 0, app.deviceComponent.getWidth(), app.deviceComponent.getHeight()));
                ((SwingDisplayComponent) app.deviceComponent.getDisplayComponent()).init();
                MIDletAccess access = MIDletBridge.getMIDletAccess();
                if (access == null) return;
                DisplayAccess displayAccess = access.getDisplayAccess();
                if (displayAccess != null) {
                    displayAccess.sizeChanged();
                    display.repaint(0, 0, display.getFullWidth(), display.getFullHeight());
                }
                app.deviceComponent.revalidate();
            }
        });
    }
}
